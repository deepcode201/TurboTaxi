using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Rides;
using TurboTaxi.Models.Routes;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.Infrastructure.Services
{ 
    public class RideService : IRideService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRedisService _redis;
        private readonly IRideNotificationService _notification;
        private readonly IRouteEstimationService _routeEstimation;
        private readonly IFareCalculatorService _fareCalculator;
        private readonly ILogger<RideService> _logger;

        public RideService(
            ApplicationDbContext db,
            IRedisService redis,
            IRideNotificationService notification,
            IRouteEstimationService routeEstimation,
            IFareCalculatorService fareCalculator,
            ILogger<RideService> logger)
        {
            _db = db;
            _redis = redis;
            _notification = notification;
            _routeEstimation = routeEstimation;
            _fareCalculator = fareCalculator;
            _logger = logger;
        }

        #region Helper Methods

        private bool IsValidCoordinate(double lat, double lng)
        {
            return lat != 0 && lng != 0 &&
                   lat >= -90 && lat <= 90 &&
                   lng >= -180 && lng <= 180;
        }

        private async Task<(double lat, double lng)> GetDriverLocationAsync(int driverId)
        {
            try
            {
                var hash = await _redis.HashGetAllAsync(RedisKeys.DriverHash(driverId));
                var dict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                double lat = 0, lng = 0;
                if (dict.TryGetValue("lat", out var latStr) && double.TryParse(latStr, out var parsedLat))
                    lat = parsedLat;
                if (dict.TryGetValue("lng", out var lngStr) && double.TryParse(lngStr, out var parsedLng))
                    lng = parsedLng;

                return (lat, lng);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get location for driver #{driverId}");
                return (0, 0);
            }
        }

        private async Task<(string? polyline, double? distanceKm, int? durationMin)> EstimateRouteAsync(
            double originLat, double originLng,
            double destLat, double destLng,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation($"?? Estimating route: ({originLat:F5}, {originLng:F5}) ? ({destLat:F5}, {destLng:F5})");

                var response = await _routeEstimation.EstimateAsync(new RouteEstimateRequest
                {
                    Origin = new Coordinate { Latitude = originLat, Longitude = originLng },
                    Destination = new Coordinate { Latitude = destLat, Longitude = destLng }
                }, ct);

                if (response.Success && response.Data != null && !string.IsNullOrWhiteSpace(response.Data.EncodedPolyline))
                {
                    _logger.LogInformation($"? Route: {response.Data.DistanceKm:F2} km, {response.Data.DurationMinutes} min");
                    return (response.Data.EncodedPolyline, response.Data.DistanceKm, response.Data.DurationMinutes);
                }

                _logger.LogWarning("?? Route estimation returned no valid data");
                return (null, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "? Route estimation failed");
                return (null, null, null);
            }
        }

        #endregion

        #region Create Ride

        public async Task<CreateRideResponse> CreateRideAsync(CreateRideRequest request, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation($"?? Creating ride for User #{request.UserId}");
                _logger.LogInformation($"   Pickup: ({request.PickupLat}, {request.PickupLng})");
                _logger.LogInformation($"   Destination: ({request.DestinationLat}, {request.DestinationLng})");

                // Ensure user exists
                var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
                if (!userExists)
                {
                    _logger.LogWarning($"?? User #{request.UserId} not found, creating test user...");

                    var newUser = new User
                    {
                        Name = "Test",
                        Surname = $"User {request.UserId}",
                        PhoneNumber = $"+994{50 + request.UserId}{1000000 + request.UserId}",
                        Email = $"testuser{request.UserId}@turbotaxi.com",
                        PasswordHash = "TestPassword123!@#",
                        IsActive = true,
                        CreatedTime = DateTime.UtcNow
                    };

                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync(ct);
                    request.UserId = newUser.Id;

                    _logger.LogInformation($"? Test user #{newUser.Id} created: {newUser.Name} {newUser.Surname}");
                }

                // Create locations
                var startLoc = new Location { Latitude = request.PickupLat, Longitude = request.PickupLng };
                var endLoc = new Location { Latitude = request.DestinationLat ?? 0, Longitude = request.DestinationLng ?? 0 };
                _db.Locations.AddRange(startLoc, endLoc);
                await _db.SaveChangesAsync(ct);

                // Create ride
                var ride = new Ride
                {
                    UserId = request.UserId,
                    StartLocationId = startLoc.Id,
                    EndLocationId = endLoc.Id,
                    RequestedVehicleType = request.VehicleType,
                    Status = RideStatus.Pending,
                    RequestedTime = DateTime.UtcNow
                };
                _db.Rides.Add(ride);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation($"? Ride #{ride.Id} created");

                // Estimate route
                var (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
                    request.PickupLat, request.PickupLng,
                    request.DestinationLat ?? 0, request.DestinationLng ?? 0,
                    ct);

                // Find nearby drivers
                _logger.LogInformation($"?? Searching for nearby drivers...");

                GeoRadiusResult[] nearbyDrivers;
                try
                {
                    nearbyDrivers = await _redis.GeoRadiusAsync(
                        $"geo:drivers:{request.VehicleType.ToString().ToLowerInvariant()}",
                        request.PickupLng,
                        request.PickupLat,
                        3.0);
                    _logger.LogInformation($"   Found {nearbyDrivers.Length} drivers within 3km");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Failed to query Redis for nearby drivers");
                    nearbyDrivers = Array.Empty<GeoRadiusResult>();
                }

                // Filter available drivers
                var candidateIds = new List<int>();
                var cutoff = DateTime.UtcNow.AddSeconds(-60);

                _logger.LogInformation($"   Cutoff time for driver check: {cutoff:O} (UTC now: {DateTime.UtcNow:O})");

                foreach (var r in nearbyDrivers)
                {
                    try
                    {
                        var did = int.Parse(r.Member);
                        var h = await _redis.HashGetAllAsync(RedisKeys.DriverHash(did));

                        if (h.Length == 0)
                        {
                            _logger.LogWarning($"   ⚠️ Driver #{did} has no data in Redis hash!");
                            continue;
                        }

                        var d = h.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                        _logger.LogInformation($"   Driver #{did} data: status='{d.GetValueOrDefault("status")}', updatedAt='{d.GetValueOrDefault("updatedAt")}', lat='{d.GetValueOrDefault("lat")}', lng='{d.GetValueOrDefault("lng")}'");

                        var hasStatus = d.TryGetValue("status", out var status);
                        var hasUpdatedAt = d.TryGetValue("updatedAt", out var updatedStr);

                        if (!hasStatus)
                        {
                            _logger.LogWarning($"   ⚠️ Driver #{did} missing 'status' field");
                            continue;
                        }

                        if (status != "Free")
                        {
                            _logger.LogInformation($"   ⏸️ Driver #{did} not free (status='{status}')");
                            continue;
                        }

                        if (!hasUpdatedAt)
                        {
                            _logger.LogWarning($"   ⚠️ Driver #{did} missing 'updatedAt' field");
                            continue;
                        }

                        if (!DateTime.TryParse(updatedStr, out var upd))
                        {
                            _logger.LogWarning($"   ⚠️ Driver #{did} invalid updatedAt format: '{updatedStr}'");
                            continue;
                        }

                        if (upd <= cutoff)
                        {
                            _logger.LogInformation($"   ⏱️ Driver #{did} stale (updatedAt={upd:O} ≤ {cutoff:O})");
                            continue;
                        }

                        candidateIds.Add(did);
                        _logger.LogInformation($"   ✓ Driver #{did} is AVAILABLE");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to check driver status for {r.Member}");
                    }
                }

                // Notify drivers
                _logger.LogInformation($"?? Notifying {candidateIds.Count} available drivers");

                // Calculate estimated fare for display to driver and user
                decimal estimatedFare = await _fareCalculator.CalculateEstimatedFareAsync(
                    request.VehicleType.ToString(),
                    distanceKm,
                    durationMin,
                    DateTime.UtcNow);

                if (candidateIds.Count > 0)
                {
                    try
                    {
                        var driverPayload = new NewRideRequestPayload
                        {
                            RideId = ride.Id,
                            VehicleType = request.VehicleType.ToString(),
                            PickupLat = request.PickupLat,
                            PickupLng = request.PickupLng,
                            DestinationLat = request.DestinationLat ?? 0,
                            DestinationLng = request.DestinationLng ?? 0,
                            EncodedPolyline = polyline,
                            EstimatedFare = estimatedFare,
                            DistanceKm = distanceKm,
                            DurationMinutes = durationMin
                        };
                        await _notification.NotifyNearestDriversAsync(candidateIds, driverPayload);
                        _logger.LogInformation($"? Notified {candidateIds.Count} drivers with ride #{ride.Id} (fare: {estimatedFare:F2} AZN, distance: {distanceKm:F2}km)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to notify drivers");
                    }
                }

                // Notify user
                try
                {
                    await _notification.NotifyUserAsync(ride.UserId, new
                    {
                        RideId = ride.Id,
                        Status = ride.Status.ToString(),
                        PickupLat = request.PickupLat,
                        PickupLng = request.PickupLng,
                        DestinationLat = request.DestinationLat,
                        DestinationLng = request.DestinationLng,
                        EncodedPolyline = polyline,
                        DistanceKm = distanceKm,
                        DurationMinutes = durationMin,
                        EstimatedFare = estimatedFare,
                        Message = "Searching for nearby drivers..."
                    }); 
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify user");
                }

                _logger.LogInformation($"? Ride #{ride.Id} creation completed successfully");

                return new CreateRideResponse
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    CandidateDriverIds = candidateIds,
                    EstimatedFare = estimatedFare,
                    DistanceKm = distanceKm,
                    DurationMinutes = durationMin
                };
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("FK_Rides_Users_UserId") == true)
            {
                _logger.LogError(ex, $"? User #{request.UserId} does not exist in database");
                throw new InvalidOperationException($"User with ID {request.UserId} does not exist", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"? Failed to create ride for User #{request.UserId}");
                throw;
            }
        }

        #endregion

        #region Accept Ride

        public async Task<RideAcceptedResponse> AcceptRideAsync(int rideId, AcceptRideRequest request, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation($"?? Driver #{request.DriverId} accepting ride #{rideId}");

                // ? CHECK IF DRIVER EXISTS IN DATABASE
                var driverExists = await _db.Drivers.AnyAsync(d => d.Id == request.DriverId, ct);
                _logger.LogInformation($"   Driver exists in DB: {driverExists}");

                if (!driverExists)
                {
                    _logger.LogError($"? Driver #{request.DriverId} not found in database!");
                    throw new KeyNotFoundException($"Driver #{request.DriverId} does not exist in database. Please create driver first.");
                }

                // Load ride with locations
                var ride = await _db.Rides
                    .Include(r => r.StartLocation)
                    .Include(r => r.EndLocation)
                    .FirstOrDefaultAsync(r => r.Id == rideId, ct);

                if (ride == null)
                {
                    _logger.LogWarning($"? Ride #{rideId} not found");
                    throw new KeyNotFoundException($"Ride #{rideId} not found");
                }

                if (ride.Status != RideStatus.Pending)
                {
                    _logger.LogWarning($"? Ride #{rideId} is not Pending (current: {ride.Status})");
                    throw new InvalidOperationException($"Ride is not in Pending state, current state: {ride.Status}");
                }

                if (ride.DriverId != null)
                {
                    _logger.LogWarning($"? Ride #{rideId} already has driver #{ride.DriverId}");
                    throw new InvalidOperationException($"Ride already assigned to driver #{ride.DriverId}");
                }

                // 🔧 CRITICAL FIX: Clear any stale driver state from previous attempts
                try
                {
                    var hKey = RedisKeys.DriverHash(request.DriverId);
                    var existingRideId = await _redis.HashGetAsync(hKey, "currentRideId");

                    if (!string.IsNullOrEmpty(existingRideId))
                    {
                        _logger.LogInformation($"⚠️ Driver #{request.DriverId} had stale ride #{existingRideId} in cache");
                        await _redis.HashDeleteAsync(hKey, "currentRideId");
                        await _redis.HashDeleteAsync(hKey, "currentUserId");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to clear stale driver state");
                }

                // Get driver location
                var (driverLat, driverLng) = await GetDriverLocationAsync(request.DriverId);
                bool hasLocation = IsValidCoordinate(driverLat, driverLng);

                _logger.LogInformation($"?? Driver location: ({driverLat:F5}, {driverLng:F5}) - Valid: {hasLocation}");

                // Estimate route from driver to pickup
                string? polyline = null;
                double? distanceKm = null;
                int? durationMin = null;

                if (hasLocation)
                {
                    (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
                        driverLat, driverLng,
                        ride.StartLocation.Latitude, ride.StartLocation.Longitude,
                        ct);
                }
                else
                {
                    _logger.LogWarning($"?? Driver #{request.DriverId} has invalid location, skipping route estimation");
                }

                // Update ride
                ride.DriverId = request.DriverId;
                ride.Status = RideStatus.Approved;
                ride.AcceptedTime = DateTime.UtcNow;
                ride.RoutePolyline = polyline;
                ride.EstimatedDistanceKm = distanceKm;
                ride.EstimatedDurationMinutes = durationMin;

                _logger.LogInformation($"   Saving ride changes to database...");
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation($"? Ride #{rideId} accepted by driver #{request.DriverId}");

                // Update driver status in Redis
                try
                {
                    await _redis.HashSetAsync(RedisKeys.DriverHash(request.DriverId), new HashEntry[]
                    {
                        new("status", "Busy"),
                        new("currentRideId", rideId.ToString()),
                        new("currentUserId", ride.UserId.ToString()),
                        new("updatedAt", DateTime.UtcNow.ToString("O"))
                    });
                    _logger.LogInformation($"? Driver #{request.DriverId} status ? Busy");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to update driver #{request.DriverId} status in Redis");
                }

                // Notify user
                try
                {
                    await _notification.NotifyUserAsync(ride.UserId, new
                    {
                        RideId = ride.Id,
                        Status = ride.Status.ToString(),
                        DriverId = ride.DriverId,
                        DriverLat = hasLocation ? driverLat : (double?)null,
                        DriverLng = hasLocation ? driverLng : (double?)null,
                        PickupLat = ride.StartLocation.Latitude,
                        PickupLng = ride.StartLocation.Longitude,
                        DestinationLat = ride.EndLocation.Latitude,
                        DestinationLng = ride.EndLocation.Longitude,
                        EncodedPolyline = polyline,
                        DistanceKm = distanceKm,
                        DurationMinutes = durationMin,
                        Message = hasLocation
                            ? $"Driver is coming! ETA: {durationMin ?? 0} min"
                            : "Driver accepted! Waiting for location..."
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to notify user #{ride.UserId}");
                }

                // Notify driver with current accepted ride info
                try
                {
                    var driverPayload = new RideCompletionPayload
                    {
                        RideId = ride.Id,
                        Status = ride.Status.ToString(), // "Approved"
                        ClearMap = false,
                        Message = "You accepted this ride! Navigate to pickup location",
                        UserType = "driver"
                    };

                    await _notification.NotifyDriverAsync(request.DriverId, driverPayload);
                    _logger.LogInformation($"✓ Driver #{request.DriverId} notified about accepted ride #{ride.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to notify driver #{request.DriverId}");
                }

                // Notify other drivers
                try
                {
                    await _notification.NotifyOtherDriversRideTaken(rideId, request.DriverId);
                    _logger.LogInformation($"? Other drivers notified that ride #{rideId} is taken");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify other drivers");
                }

                return new RideAcceptedResponse
                {
                    RideId = ride.Id,
                    DriverId = request.DriverId,
                    Status = ride.Status.ToString()
                };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"? Database error while accepting ride #{rideId}");
                _logger.LogError($"   Inner Exception: {ex.InnerException?.Message}");
                _logger.LogError($"   Stack Trace: {ex.InnerException?.StackTrace}");
                throw new InvalidOperationException($"Database error: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"? Unexpected error accepting ride #{rideId}");
                throw;
            }
        }

        #endregion

        #region Start Ride

        public async Task<StartRideResponse> StartRideAsync(int rideId, StartRideRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"?? Driver #{request.DriverId} starting ride #{rideId}");

            var ride = await _db.Rides
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .FirstOrDefaultAsync(r => r.Id == rideId, ct);

            if (ride == null)
            {
                _logger.LogWarning($"? Ride #{rideId} not found");
                throw new KeyNotFoundException($"Ride #{rideId} not found");
            }

            if (ride.Status != RideStatus.Approved)
            {
                _logger.LogWarning($"? Ride #{rideId} is not Approved (current: {ride.Status})");
                throw new InvalidOperationException($"Ride is not in Approved state, current state: {ride.Status}");
            }

            if (ride.DriverId != request.DriverId)
            {
                _logger.LogWarning($"? Driver mismatch: expected #{ride.DriverId}, got #{request.DriverId}");
                throw new InvalidOperationException($"Driver mismatch");
            }

            // Get driver location
            var (driverLat, driverLng) = await GetDriverLocationAsync(request.DriverId);
            bool hasLocation = IsValidCoordinate(driverLat, driverLng);

            _logger.LogInformation($"?? Driver location: ({driverLat:F5}, {driverLng:F5}) - Valid: {hasLocation}");

            // Estimate route to destination
            string? polyline = null;
            double? distanceKm = null;
            int? durationMin = null;

            if (hasLocation)
            {
                (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
                    driverLat, driverLng,
                    ride.EndLocation.Latitude, ride.EndLocation.Longitude,
                    ct);
            }
            else
            {
                _logger.LogWarning($"?? Driver #{request.DriverId} has invalid location");
            }

            // Update ride
            ride.Status = RideStatus.OnWay;
            ride.PickUpTime = DateTime.UtcNow;
            ride.RoutePolyline = polyline;
            ride.EstimatedDistanceKm = distanceKm;
            ride.EstimatedDurationMinutes = durationMin;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation($"? Ride #{rideId} started");

            // Notify user
            try
            {
                await _notification.NotifyUserAsync(ride.UserId, new
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    StartedAt = ride.PickUpTime,
                    DriverLat = hasLocation ? driverLat : (double?)null,
                    DriverLng = hasLocation ? driverLng : (double?)null,
                    PickupLat = ride.StartLocation.Latitude,
                    PickupLng = ride.StartLocation.Longitude,
                    DestinationLat = ride.EndLocation.Latitude,
                    DestinationLng = ride.EndLocation.Longitude,
                    EncodedPolyline = polyline,
                    DistanceKm = distanceKm,
                    DurationMinutes = durationMin,
                    Message = $"Trip started! ETA: {durationMin ?? 0} min"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to notify user #{ride.UserId}");
            }

            // Notify driver
            try
            {
                await _notification.NotifyDriverAsync(request.DriverId, new
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    StartedAt = ride.PickUpTime,
                    DestinationLat = ride.EndLocation.Latitude,
                    DestinationLng = ride.EndLocation.Longitude,
                    EncodedPolyline = polyline,
                    DistanceKm = distanceKm,
                    DurationMinutes = durationMin,
                    Message = "Trip started - navigate to destination"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to notify driver #{request.DriverId}");
            }

            return new StartRideResponse
            {
                RideId = ride.Id,
                Status = ride.Status.ToString(),
                StartedAtUtc = ride.PickUpTime.Value
            };
        }

        #endregion

        #region Finish Ride

        public async Task<RideFinishedResponse> FinishRideAsync(int rideId, FinishRideRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"?? Driver #{request.DriverId} finishing ride #{rideId}");

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == rideId, ct);

            if (ride == null)
            {
                _logger.LogWarning($"? Ride #{rideId} not found");
                throw new KeyNotFoundException($"Ride #{rideId} not found");
            }

            if (ride.Status != RideStatus.OnWay)
            {
                _logger.LogWarning($"? Ride #{rideId} is not OnWay (current: {ride.Status})");
                throw new InvalidOperationException($"Ride is not in OnWay state, current state: {ride.Status}");
            }

            if (ride.DriverId != request.DriverId)
            {
                _logger.LogWarning($"? Driver mismatch: expected #{ride.DriverId}, got #{request.DriverId}");
                throw new InvalidOperationException($"Driver mismatch");
            }

            // Update ride - Use EstimatedFare as final price
            ride.Status = RideStatus.Completed;
            ride.CompletedTime = DateTime.UtcNow;
            ride.ActualFare = ride.EstimatedFare ?? 0;  // 🔧 Use estimated fare, not driver input

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation($"✓ Ride #{rideId} completed - Fare: {ride.ActualFare:F2} AZN (estimated at creation)");

            // Update driver status in Redis
            try
            {
                var hKey = RedisKeys.DriverHash(request.DriverId);
                await _redis.HashSetAsync(hKey, new HashEntry[]
                {
                    new("status", "Free"),
                    new("updatedAt", DateTime.UtcNow.ToString("O"))
                });
                await _redis.HashDeleteAsync(hKey, "currentRideId");
                await _redis.HashDeleteAsync(hKey, "currentUserId");

                _logger.LogInformation($"✓ Driver #{request.DriverId} status → Free");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update driver #{request.DriverId} status in Redis");
            }

            // 🔧 Notify user with ride completion details
            try
            {
                var userPayload = new RideCompletionPayload
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    FinalPrice = ride.ActualFare.Value,
                    CompletedAtUtc = ride.CompletedTime.Value,
                    ClearMap = true,
                    Message = $"✓ Trip completed! Total: {ride.ActualFare:F2} AZN. Thank you!",
                    UserType = "user"
                };
                await _notification.NotifyUserAsync(ride.UserId, userPayload);
                _logger.LogInformation($"✓ User #{ride.UserId} notified about ride completion");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to notify user #{ride.UserId}");
            }

            // Notify driver
            try
            {
                var driverPayload = new RideCompletionPayload
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    FinalPrice = ride.ActualFare.Value,
                    CompletedAtUtc = ride.CompletedTime.Value,
                    ClearMap = true,
                    Message = $"✓ Trip completed! You earned {ride.ActualFare:F2} AZN",
                    UserType = "driver"
                };
                await _notification.NotifyDriverAsync(request.DriverId, driverPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to notify driver #{request.DriverId}");
            }

            return new RideFinishedResponse
            {
                RideId = ride.Id,
                Status = ride.Status.ToString(),
                FinalPrice = ride.ActualFare.Value,
                FinishedAtUtc = ride.CompletedTime.Value
            };
        }

        #endregion

        #region Cancel Operations

        public async Task<CancelRideResponse> CancelRideByUserAsync(int rideId, CancelRideByUserRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"? User #{request.UserId} canceling ride #{rideId}");

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == rideId, ct);

            if (ride == null)
                throw new KeyNotFoundException($"Ride #{rideId} not found");

            if (ride.UserId != request.UserId)
                throw new InvalidOperationException($"User mismatch");

            if (ride.Status == RideStatus.Completed)
                throw new InvalidOperationException($"Cannot cancel completed ride");

            var prevStatus = ride.Status;

            // Update ride status FIRST
            ride.Status = RideStatus.Canceled;
            ride.CanceledTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Free driver if assigned
            if (ride.DriverId.HasValue)
            {
                try
                {
                    var hKey = RedisKeys.DriverHash(ride.DriverId.Value);
                    await _redis.HashSetAsync(hKey, new HashEntry[]
                    {
                        new("status", "Free"),
                        new("updatedAt", DateTime.UtcNow.ToString("O"))
                    });
                    await _redis.HashDeleteAsync(hKey, "currentRideId");
                    await _redis.HashDeleteAsync(hKey, "currentUserId");

                    // 🔧 FIX: Notify driver with RideStatusUpdated (not just NotifyDriver)
                    var driverCancelPayload = new RideCompletionPayload
                    {
                        RideId = ride.Id,
                        Status = ride.Status.ToString(),
                        ClearMap = true,
                        Message = "User canceled this ride"
                    };
                    await _notification.NotifyDriverAsync(ride.DriverId.Value, driverCancelPayload);

                    _logger.LogInformation($"✓ Driver #{ride.DriverId} notified about cancellation and freed from ride #{rideId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to free driver #{ride.DriverId} or send notification");
                }
            }

            // If pending and no driver, notify all drivers and clean up notification keys
            if (prevStatus == RideStatus.Pending && !ride.DriverId.HasValue)
            {
                try
                {
                    // Clean up any notification keys for this ride
                    await _redis.DeleteKeysByPatternAsync($"notify:ride:{rideId}:driver:*");
                    _logger.LogInformation($"? Cleaned up notification keys for ride #{rideId}");

                    await _notification.NotifyAllDriversRideCanceled(rideId);
                    _logger.LogInformation($"? All drivers notified about ride #{rideId} cancellation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify drivers or clean up keys");
                }
            }

            _logger.LogInformation($"✓ Ride #{rideId} canceled by user #{request.UserId}");

            return new CancelRideResponse
            {
                RideId = ride.Id,
                Status = ride.Status.ToString()
            };
        }

        public async Task<CancelRideResponse> CancelRideByDriverAsync(int rideId, CancelRideByDriverRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"? Driver #{request.DriverId} declining ride #{rideId}");

            var ride = await _db.Rides
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .FirstOrDefaultAsync(r => r.Id == rideId, ct);

            if (ride == null)
                throw new KeyNotFoundException($"Ride #{rideId} not found");

            if (ride.DriverId != request.DriverId)
                throw new InvalidOperationException($"Driver mismatch");

            if (ride.Status == RideStatus.Completed)
                throw new InvalidOperationException($"Cannot cancel completed ride");

            if (ride.Status == RideStatus.Canceled)
                throw new InvalidOperationException($"Ride already canceled");

            // Free the driver first
            try
            {
                var hKey = RedisKeys.DriverHash(request.DriverId);
                await _redis.HashSetAsync(hKey, new HashEntry[]
                {
                    new("status", "Free"),
                    new("updatedAt", DateTime.UtcNow.ToString("O"))
                });
                await _redis.HashDeleteAsync(hKey, "currentRideId");
                await _redis.HashDeleteAsync(hKey, "currentUserId");

                _logger.LogInformation($"✓ Driver #{request.DriverId} freed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to free driver #{request.DriverId}");
            }

            // 🔧 NEW LOGIC: Instead of canceling the ride, reset it to Pending and search for another driver
            try
            {
                _logger.LogInformation($"? Resetting ride #{rideId} to Pending status for reassignment");

                // Reset ride status to Pending (as if just created)
                ride.Status = RideStatus.Pending;
                ride.DriverId = null; // Remove current driver
                ride.AcceptedTime = null;
                ride.PickUpTime = null;
                ride.RequestedTime = DateTime.UtcNow; // Reset request time for fresh search
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation($"✓ Ride #{rideId} reset to Pending");

                // Find nearby drivers again
                var (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
                    ride.StartLocation.Latitude, ride.StartLocation.Longitude,
                    ride.EndLocation.Latitude, ride.EndLocation.Longitude,
                    ct);

                var nearbyDrivers = await _redis.GeoRadiusAsync(
                    $"geo:drivers:{ride.RequestedVehicleType.ToString().ToLowerInvariant()}",
                    ride.StartLocation.Longitude,
                    ride.StartLocation.Latitude,
                    3.0);

                _logger.LogInformation($"?? Found {nearbyDrivers.Length} drivers for reassignment");

                // Filter available drivers (exclude the driver who just declined)
                var candidateIds = new List<int>();
                var cutoff = DateTime.UtcNow.AddSeconds(-60);

                foreach (var r in nearbyDrivers)
                {
                    try
                    {
                        var did = int.Parse(r.Member);

                        // Skip the driver who declined
                        if (did == request.DriverId)
                        {
                            _logger.LogInformation($"   ⏩ Skipping driver #{did} (declined this ride)");
                            continue;
                        }

                        var h = await _redis.HashGetAllAsync(RedisKeys.DriverHash(did));

                        if (h.Length == 0)
                            continue;

                        var d = h.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                        if (d.TryGetValue("status", out var status) && status == "Free" &&
                            d.TryGetValue("updatedAt", out var updatedStr) &&
                            DateTime.TryParse(updatedStr, out var upd) &&
                            upd > cutoff)
                        {
                            candidateIds.Add(did);
                            _logger.LogInformation($"   ✓ Driver #{did} is available for reassignment");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to check driver availability");
                    }
                }

                // Send new ride request to available drivers
                if (candidateIds.Count > 0)
                {
                    _logger.LogInformation($"?? Notifying {candidateIds.Count} drivers about reassigned ride");

                    var estimatedFare = await _fareCalculator.CalculateEstimatedFareAsync(
                        ride.RequestedVehicleType.ToString(),
                        distanceKm ?? 0,
                        durationMin ?? 0,
                        DateTime.UtcNow);

                    var driverPayload = new NewRideRequestPayload
                    {
                        RideId = ride.Id,
                        VehicleType = ride.RequestedVehicleType.ToString(),
                        PickupLat = ride.StartLocation.Latitude,
                        PickupLng = ride.StartLocation.Longitude,
                        DestinationLat = ride.EndLocation.Latitude,
                        DestinationLng = ride.EndLocation.Longitude,
                        EncodedPolyline = polyline,
                        EstimatedFare = estimatedFare,
                        DistanceKm = distanceKm ?? 0,
                        DurationMinutes = durationMin ?? 0
                    };

                    await _notification.NotifyNearestDriversAsync(candidateIds, driverPayload);
                    _logger.LogInformation($"✓ Ride #{rideId} reassigned to {candidateIds.Count} drivers");

                    // Notify user that a new driver is being searched
                    var userPayload = new RideCompletionPayload
                    {
                        RideId = ride.Id,
                        Status = "Pending",
                        ClearMap = false,
                        Message = "Driver not available. Searching for another driver..."
                    };
                    await _notification.NotifyUserAsync(ride.UserId, userPayload);
                    _logger.LogInformation($"✓ User #{ride.UserId} notified about driver reassignment");
                }
                else
                {
                    _logger.LogWarning($"⚠️ No available drivers found for reassignment of ride #{rideId}");

                    var userPayload = new RideCompletionPayload
                    {
                        RideId = ride.Id,
                        Status = "Pending",
                        ClearMap = false,
                        Message = "Driver not available. Please wait while we search for another driver..."
                    };
                    await _notification.NotifyUserAsync(ride.UserId, userPayload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to reassign ride #{rideId}");
                throw;
            }

            _logger.LogInformation($"✓ Ride #{rideId} reassignment process completed");

            return new CancelRideResponse
            {
                RideId = ride.Id,
                Status = "Pending" // Ride is now open for other drivers
            };
        }

        #endregion
    }
}
