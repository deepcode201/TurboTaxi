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
        private readonly ILogger<RideService> _logger;

        public RideService(
            ApplicationDbContext db,
            IRedisService redis,
            IRideNotificationService notification,
            IRouteEstimationService routeEstimation,
            ILogger<RideService> logger)
        {
            _db = db;
            _redis = redis;
            _notification = notification;
            _routeEstimation = routeEstimation;
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
                        FullName = $"Test User {request.UserId}",
                        PhoneNumber = $"+994{50 + request.UserId}{1000000 + request.UserId}",
                        Email = $"testuser{request.UserId}@turbotaxi.com",
                        PasswordHash = "TestPassword123!@#",
                        IsActive = true,
                        CreatedTime = DateTime.UtcNow
                    };

                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync(ct);
                    request.UserId = newUser.Id;

                    _logger.LogInformation($"? Test user #{newUser.Id} created: {newUser.FullName}");
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
                    RequestedVehicleType = Enum.TryParse<VehicleType>(request.VehicleType, true, out var vt) ? vt : VehicleType.Standard,
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
                        $"geo:drivers:{request.VehicleType.ToLowerInvariant()}",
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

                foreach (var r in nearbyDrivers)
                {
                    try
                    {
                        var did = int.Parse(r.Member);
                        var h = await _redis.HashGetAllAsync(RedisKeys.DriverHash(did));
                        var d = h.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                        if (d.TryGetValue("status", out var status) && status == "Free" &&
                            d.TryGetValue("updatedAt", out var updatedStr) &&
                            DateTime.TryParse(updatedStr, out var upd) && upd > cutoff)
                        {
                            candidateIds.Add(did);
                            _logger.LogInformation($"   ? Driver #{did} is available");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to check driver status for {r.Member}");
                    }
                }

                // Notify drivers
                _logger.LogInformation($"?? Notifying {candidateIds.Count} available drivers");

                var notifyIds = new List<int>();
                foreach (var driverId in candidateIds)
                {
                    try
                    {
                        var key = $"notify:ride:{ride.Id}:driver:{driverId}";
                        var firstTime = await _redis.SetStringIfNotExistsAsync(key, "1", TimeSpan.FromHours(6));
                        if (firstTime) notifyIds.Add(driverId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to set notification key for driver #{driverId}");
                    }
                }

                if (notifyIds.Count > 0)
                {
                    try
                    {
                        await _notification.NotifyNearestDriversAsync(notifyIds, new
                        {
                            RideId = ride.Id,
                            PickupLat = request.PickupLat,
                            PickupLng = request.PickupLng,
                            DestinationLat = request.DestinationLat,
                            DestinationLng = request.DestinationLng,
                            VehicleType = request.VehicleType,
                            EncodedPolyline = polyline,
                            DistanceKm = distanceKm,
                            DurationMinutes = durationMin
                        });
                        _logger.LogInformation($"? Notified {notifyIds.Count} drivers");
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
                    CandidateDriverIds = candidateIds
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

                // Notify driver
                try
                {
                    await _notification.NotifyDriverAsync(request.DriverId, new
                    {
                        RideId = ride.Id,
                        Status = ride.Status.ToString(),
                        PickupLat = ride.StartLocation.Latitude,
                        PickupLng = ride.StartLocation.Longitude,
                        DestinationLat = ride.EndLocation.Latitude,
                        DestinationLng = ride.EndLocation.Longitude,
                        EncodedPolyline = polyline,
                        DistanceKm = distanceKm,
                        DurationMinutes = durationMin,
                        Message = "Navigate to pickup location"
                    });
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

            // Update ride
            ride.Status = RideStatus.Completed;
            ride.CompletedTime = DateTime.UtcNow;
            ride.ActualFare = request.FinalPrice;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation($"? Ride #{rideId} completed - Fare: {request.FinalPrice} AZN");

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

                _logger.LogInformation($"? Driver #{request.DriverId} status ? Free");
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
                    FinalPrice = ride.ActualFare,
                    CompletedAt = ride.CompletedTime,
                    ClearMap = true,
                    EncodedPolyline = (string?)null,
                    Message = $"Trip completed! Total: {ride.ActualFare:F2} AZN. Thank you!"
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
                    FinalPrice = ride.ActualFare,
                    CompletedAt = ride.CompletedTime,
                    ClearMap = true,
                    EncodedPolyline = (string?)null,
                    Message = $"Trip completed! You earned {ride.ActualFare:F2} AZN"
                });
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

                    await _notification.NotifyDriverAsync(ride.DriverId.Value, new
                    {
                        RideId = ride.Id,
                        Status = "CanceledByUser",
                        ClearMap = true,
                        Message = "User canceled this ride"
                    });

                    _logger.LogInformation($"? Driver #{ride.DriverId} freed from ride #{rideId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to free driver #{ride.DriverId}");
                }
            }

            ride.Status = RideStatus.Canceled;
            ride.CanceledTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // If pending and no driver, notify all drivers
            if (prevStatus == RideStatus.Pending && !ride.DriverId.HasValue)
            {
                try
                {
                    await _notification.NotifyAllDriversRideCanceled(rideId);
                    _logger.LogInformation($"? All drivers notified about ride #{rideId} cancellation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify drivers");
                }
            }

            _logger.LogInformation($"? Ride #{rideId} canceled by user");

            return new CancelRideResponse
            {
                RideId = ride.Id,
                Status = ride.Status.ToString()
            };
        }

        public async Task<CancelRideResponse> CancelRideByDriverAsync(int rideId, CancelRideByDriverRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"? Driver #{request.DriverId} canceling ride #{rideId}");

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == rideId, ct);

            if (ride == null)
                throw new KeyNotFoundException($"Ride #{rideId} not found");

            if (ride.DriverId != request.DriverId)
                throw new InvalidOperationException($"Driver mismatch");

            if (ride.Status == RideStatus.Completed)
                throw new InvalidOperationException($"Cannot cancel completed ride");

            // Free driver
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

                _logger.LogInformation($"? Driver #{request.DriverId} status ? Free");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update driver #{request.DriverId} status");
            }

            ride.Status = RideStatus.Canceled;
            ride.CanceledTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Notify user
            try
            {
                await _notification.NotifyUserAsync(ride.UserId, new
                {
                    RideId = ride.Id,
                    Status = ride.Status.ToString(),
                    CanceledBy = "Driver",
                    ClearMap = true,
                    Message = "Driver canceled this ride. Searching for new driver..."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to notify user #{ride.UserId}");
            }

            _logger.LogInformation($"? Ride #{rideId} canceled by driver");

            return new CancelRideResponse
            {
                RideId = ride.Id,
                Status = ride.Status.ToString()
            };
        }

        #endregion
    }
}
