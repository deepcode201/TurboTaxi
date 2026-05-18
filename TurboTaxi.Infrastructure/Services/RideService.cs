using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.PromoCodes;
using TurboTaxi.Models.Rides;
using TurboTaxi.Models.Routes;
using TurboTaxi.Models.Trips;
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
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<RideService> _logger;

        public RideService(
            ApplicationDbContext db,
            IRedisService redis,
            IRideNotificationService notification,
            IRouteEstimationService routeEstimation,
            IFareCalculatorService fareCalculator,
            IPromoCodeService promoCodeService,
            ILogger<RideService> logger)
        {
            _db = db;
            _redis = redis;
            _notification = notification;
            _routeEstimation = routeEstimation;
            _fareCalculator = fareCalculator;
            _promoCodeService = promoCodeService;
            _logger = logger;
        }

        #region Helper Methods

        private bool IsValidCoordinate(double lat, double lng)
        {
            return lat != 0 && lng != 0 &&
                   lat >= -90 && lat <= 90 &&
                   lng >= -180 && lng <= 180;
        }

        private static bool IsDriverAvailableForNewRide(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return true;

            if (Enum.TryParse<DriverStatus>(status, ignoreCase: true, out var parsed))
                return parsed is DriverStatus.Free or DriverStatus.BusySoon;

            return string.Equals(status, nameof(DriverStatus.Free), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, nameof(DriverStatus.BusySoon), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDriverLocationFresh(string? updatedStr, DateTime cutoffUtc)
        {
            if (string.IsNullOrWhiteSpace(updatedStr))
                return true;

            if (DateTimeOffset.TryParse(updatedStr, out var offset))
            {
                var upd = offset.UtcDateTime;
                return upd > cutoffUtc;
            }

            if (DateTime.TryParse(updatedStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var updDt))
            {
                if (updDt.Kind == DateTimeKind.Unspecified)
                    updDt = DateTime.SpecifyKind(updDt, DateTimeKind.Utc);
                else if (updDt.Kind == DateTimeKind.Local)
                    updDt = updDt.ToUniversalTime();

                return updDt > cutoffUtc;
            }

            return false;
        }

        private static int NormalizePageNumber(int pageNumber)
        {
            return pageNumber < 1 ? 1 : pageNumber;
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize < 1)
                return 20;

            return pageSize > 100 ? 100 : pageSize;
        }

        private static string? FormatAddress(Location location)
        {
            var parts = new[] { location.Name, location.AddressLine, location.City, location.Country }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            if (parts.Length > 0)
            {
                return string.Join(", ", parts);
            }

            return $"{location.Latitude:F6}, {location.Longitude:F6}";
        }

        private static DateTime? ResolveRideDateUtc(Ride ride)
        {
            return ride.CompletedTime
                   ?? ride.CanceledTime
                   ?? ride.PickUpTime
                   ?? ride.AcceptedTime
                   ?? ride.RequestedTime;
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
                    _logger.LogInformation(
                        "Route via {Source}: {Dist:F2} km, {Min} min, polyline {Len} chars",
                        response.Data.Source,
                        response.Data.DistanceKm,
                        response.Data.DurationMinutes,
                        response.Data.EncodedPolyline.Length);
                    return (response.Data.EncodedPolyline, response.Data.DistanceKm, response.Data.DurationMinutes);
                }

                _logger.LogError("Route estimation failed: {Message}", response.Message);
                return (null, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Route estimation failed");
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

                // Estimate route (pickup -> destination)
                var (polyline, distanceKm, durationMin) = await EstimateRouteAsync(
                    request.PickupLat, request.PickupLng,
                    request.DestinationLat ?? 0, request.DestinationLng ?? 0,
                    ct);

                ride.RoutePolyline = polyline;
                ride.EstimatedDistanceKm = distanceKm;
                ride.EstimatedDurationMinutes = durationMin;
                await _db.SaveChangesAsync(ct);

                // Find nearby drivers
                _logger.LogInformation($"?? Searching for nearby drivers...");

                GeoRadiusResult[] nearbyDrivers;
                try
                {
                    nearbyDrivers = await _redis.GeoRadiusAsync(
                        $"geo:drivers:{request.VehicleType.ToString().ToLowerInvariant()}",
                        request.PickupLng,
                        request.PickupLat,
                        5.0);
                    _logger.LogInformation($"   Found {nearbyDrivers.Length} drivers within 5km");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Failed to query Redis for nearby drivers");
                    nearbyDrivers = Array.Empty<GeoRadiusResult>();
                }

                // Filter available drivers
                var candidateIds = new List<int>();
                var cutoff = DateTime.UtcNow.AddSeconds(-120);

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

                        if (hasStatus && !IsDriverAvailableForNewRide(status))
                        {
                            _logger.LogInformation($"   ⏸️ Driver #{did} not available (status='{status}')");
                            continue;
                        }

                        if (hasUpdatedAt && !IsDriverLocationFresh(updatedStr, cutoff))
                        {
                            _logger.LogInformation($"   ⏱️ Driver #{did} stale (updatedAt='{updatedStr}', cutoff={cutoff:O})");
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

                decimal estimatedFare;
                try
                {
                    estimatedFare = await _fareCalculator.CalculateEstimatedFareAsync(
                        request.VehicleType.ToString(),
                        distanceKm,
                        durationMin,
                        DateTime.UtcNow,
                        "Baku");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fare calculation failed for ride #{RideId}, using default", ride.Id);
                    estimatedFare = 10m;
                }

                ride.EstimatedFare = estimatedFare;
                await _db.SaveChangesAsync(ct);

                var fareForUser = estimatedFare;
                var promoPreview = new PromoFareResult
                {
                    Applied = false,
                    OriginalFare = estimatedFare,
                    FinalFare = estimatedFare
                };

                try
                {
                    promoPreview = await _promoCodeService.ApplyActivePromoAsync(
                        request.UserId,
                        estimatedFare,
                        consumeUsage: false,
                        ct);
                    fareForUser = promoPreview.Applied ? promoPreview.FinalFare : estimatedFare;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Promo preview failed for ride #{RideId}; notifying drivers anyway", ride.Id);
                }

                _logger.LogInformation($"?? Notifying {candidateIds.Count} matched drivers (+ all_drivers broadcast)");

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
                        EstimatedFare = fareForUser,
                        DistanceKm = distanceKm,
                        DurationMinutes = durationMin
                    };

                    await _notification.NotifyNearestDriversAsync(candidateIds, driverPayload);
                    _logger.LogInformation(
                        "? Ride #{RideId} notification sent ({Count} matched drivers, fare: {Fare:F2} AZN)",
                        ride.Id, candidateIds.Count, fareForUser);

                    if (candidateIds.Count == 0)
                    {
                        _logger.LogWarning(
                            "No drivers matched for ride #{RideId} (vehicle={VehicleType}, geo count={GeoCount})",
                            ride.Id, request.VehicleType, nearbyDrivers.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify drivers for ride #{RideId}", ride.Id);
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
                        TripEncodedPolyline = polyline,
                        PolylinePhase = "Trip",
                        DistanceKm = distanceKm,
                        DurationMinutes = durationMin,
                        EstimatedFare = fareForUser,
                        OriginalEstimatedFare = estimatedFare,
                        DiscountAmount = promoPreview.DiscountAmount,
                        PromoApplied = promoPreview.Applied,
                        PromoCode = promoPreview.PromoCode,
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
                    EstimatedFare = fareForUser,
                    OriginalEstimatedFare = estimatedFare,
                    DiscountAmount = promoPreview.Applied ? promoPreview.DiscountAmount : null,
                    PromoApplied = promoPreview.Applied,
                    PromoCode = promoPreview.PromoCode,
                    PromoRemainingRides = promoPreview.RemainingRideCount,
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

                if (!hasLocation)
                {
                    _logger.LogWarning($"?? Driver #{request.DriverId} has no valid location. Ask driver to send location before accepting ride.");
                    throw new InvalidOperationException("Driver location is not available. Send a location update before accepting the ride.");
                }

                // Estimate route from driver to pickup
                string? approachPolyline;
                double? approachDistanceKm;
                int? approachDurationMin;

                (approachPolyline, approachDistanceKm, approachDurationMin) = await EstimateRouteAsync(
                    driverLat, driverLng,
                    ride.StartLocation.Latitude, ride.StartLocation.Longitude,
                    ct);

                if (string.IsNullOrWhiteSpace(approachPolyline))
                {
                    _logger.LogError("Could not compute driver->pickup route for ride #{RideId}", rideId);
                    throw new InvalidOperationException("Could not calculate route to pickup. Please try again.");
                }

                // Keep trip polyline (pickup -> destination) for user map overview
                var tripPolyline = ride.RoutePolyline;
                if (string.IsNullOrWhiteSpace(tripPolyline))
                {
                    (tripPolyline, _, _) = await EstimateRouteAsync(
                        ride.StartLocation.Latitude, ride.StartLocation.Longitude,
                        ride.EndLocation.Latitude, ride.EndLocation.Longitude,
                        ct);
                }

                // Update ride
                ride.DriverId = request.DriverId;
                ride.Status = RideStatus.Approved;
                ride.AcceptedTime = DateTime.UtcNow;
                ride.RoutePolyline = approachPolyline;
                ride.EstimatedDistanceKm = approachDistanceKm;
                ride.EstimatedDurationMinutes = approachDurationMin;

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
                        DriverLat = driverLat,
                        DriverLng = driverLng,
                        PickupLat = ride.StartLocation.Latitude,
                        PickupLng = ride.StartLocation.Longitude,
                        DestinationLat = ride.EndLocation.Latitude,
                        DestinationLng = ride.EndLocation.Longitude,
                        EncodedPolyline = approachPolyline,
                        TripEncodedPolyline = tripPolyline,
                        PolylinePhase = "Approach",
                        DistanceKm = approachDistanceKm,
                        DurationMinutes = approachDurationMin,
                        Message = $"Driver is coming! ETA: {approachDurationMin ?? 0} min"
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
                        EncodedPolyline = approachPolyline,
                        TripEncodedPolyline = tripPolyline,
                        PolylinePhase = "Approach",
                        DistanceKm = approachDistanceKm,
                        DurationMinutes = approachDurationMin,
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
                    TripEncodedPolyline = polyline,
                    PolylinePhase = "EnRoute",
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
                    TripEncodedPolyline = polyline,
                    PolylinePhase = "EnRoute",
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

            // Final fare + active promo (consumes one ride from ExpireRideCount)
            var originalFare = await ResolveFinalFareAsync(ride, ct);
            var promo = await _promoCodeService.ApplyActivePromoAsync(
                ride.UserId,
                originalFare,
                consumeUsage: true,
                ct);

            var chargedFare = promo.Applied ? promo.FinalFare : originalFare;

            ride.EstimatedFare ??= originalFare;
            ride.Status = RideStatus.Completed;
            ride.CompletedTime = DateTime.UtcNow;
            ride.ActualFare = chargedFare;
            ride.DiscountAmount = promo.Applied ? promo.DiscountAmount : null;
            ride.PromoCodeId = promo.Applied ? promo.PromoCodeId : null;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "✓ Ride #{RideId} completed - Original: {Original:F2} AZN, Charged: {Charged:F2} AZN, Promo: {Promo}",
                rideId, originalFare, chargedFare, promo.Applied ? promo.PromoCode : "none");

            // Update driver status in Redis
            try
            {
                var hKey = RedisKeys.DriverHash(request.DriverId);
                var currentHash = await _redis.HashGetAllAsync(hKey);
                var driverData = currentHash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                // Get last known location to preserve availability
                var lastLat = driverData.GetValueOrDefault("lat") ?? "0";
                var lastLng = driverData.GetValueOrDefault("lng") ?? "0";
                var vehicleType = driverData.GetValueOrDefault("vehicleType") ?? "standard";

                var now = DateTime.UtcNow;
                await _redis.HashSetAsync(hKey, new HashEntry[]
                {
                    new("status", "Free"),
                    new("updatedAt", now.ToString("O")),
                    new("lat", lastLat),
                    new("lng", lastLng),
                    new("vehicleType", vehicleType)
                });
                await _redis.HashDeleteAsync(hKey, "currentRideId");
                await _redis.HashDeleteAsync(hKey, "currentUserId");

                _logger.LogInformation($"✓ Driver #{request.DriverId} status → Free (lat={lastLat}, lng={lastLng})");
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
                    FinalPrice = chargedFare,
                    CompletedAtUtc = ride.CompletedTime.Value,
                    ClearMap = true,
                    Message = $"✓ Trip completed! Total: {chargedFare:F2} AZN. Thank you!",
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
                    FinalPrice = chargedFare,
                    CompletedAtUtc = ride.CompletedTime.Value,
                    ClearMap = true,
                    Message = $"✓ Trip completed! You earned {chargedFare:F2} AZN",
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
                FinalPrice = chargedFare,
                OriginalFare = originalFare,
                DiscountAmount = promo.Applied ? promo.DiscountAmount : null,
                PromoApplied = promo.Applied,
                PromoCode = promo.PromoCode,
                PromoRemainingRides = promo.RemainingRideCount,
                FinishedAtUtc = ride.CompletedTime.Value
            };
        }

        /// <summary>
        /// Returns persisted trip fare, or recalculates from pickup→destination when missing (legacy rides).
        /// </summary>
        private async Task<decimal> ResolveFinalFareAsync(Ride ride, CancellationToken ct)
        {
            if (ride.EstimatedFare.HasValue && ride.EstimatedFare.Value > 0)
                return ride.EstimatedFare.Value;

            _logger.LogWarning(
                "Ride #{RideId} has no EstimatedFare — recalculating from trip route",
                ride.Id);

            var rideWithLocations = await _db.Rides
                .AsNoTracking()
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .FirstOrDefaultAsync(r => r.Id == ride.Id, ct);

            double? distanceKm = ride.EstimatedDistanceKm;
            int? durationMin = ride.EstimatedDurationMinutes;

            if (rideWithLocations?.StartLocation != null && rideWithLocations.EndLocation != null)
            {
                var (_, dist, dur) = await EstimateRouteAsync(
                    rideWithLocations.StartLocation.Latitude,
                    rideWithLocations.StartLocation.Longitude,
                    rideWithLocations.EndLocation.Latitude,
                    rideWithLocations.EndLocation.Longitude,
                    ct);

                if (dist.HasValue)
                    distanceKm = dist;
                if (dur.HasValue)
                    durationMin = dur;
            }

            return await _fareCalculator.CalculateEstimatedFareAsync(
                ride.RequestedVehicleType.ToString(),
                distanceKm,
                durationMin,
                ride.PickUpTime ?? ride.RequestedTime ?? DateTime.UtcNow,
                "Baku");
        }

        #endregion

        #region Cancel Operations

        public async Task<CancelRideResponse> CancelRideByUserAsync(int rideId, CancelRideByUserRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation($"? User #{request.UserId} canceling ride #{rideId}");

            var ride = await _db.Rides
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .FirstOrDefaultAsync(r => r.Id == rideId, ct);

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

            // Free driver if assigned and notify them
            if (ride.DriverId.HasValue)
            {
                try
                {
                    var hKey = RedisKeys.DriverHash(ride.DriverId.Value);
                    var currentHash = await _redis.HashGetAllAsync(hKey);
                    var driverData = currentHash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                    // Get last known location to preserve availability
                    var lastLat = driverData.GetValueOrDefault("lat") ?? "0";
                    var lastLng = driverData.GetValueOrDefault("lng") ?? "0";
                    var vehicleType = driverData.GetValueOrDefault("vehicleType") ?? "standard";

                    var now = DateTime.UtcNow;
                    await _redis.HashSetAsync(hKey, new HashEntry[]
                    {
                        new("status", "Free"),
                        new("updatedAt", now.ToString("O")),
                        new("lat", lastLat),
                        new("lng", lastLng),
                        new("vehicleType", vehicleType)
                    });
                    await _redis.HashDeleteAsync(hKey, "currentRideId");
                    await _redis.HashDeleteAsync(hKey, "currentUserId");

                    // Notify driver about cancellation
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

            // Notify all drivers about ride cancellation to remove it from their notifications
            try
            {
                // Clean up notification keys from Redis
                await _redis.DeleteKeysByPatternAsync($"notify:ride:{rideId}:driver:*");
                _logger.LogInformation($"✓ Cleaned up notification keys for ride #{rideId}");

                // Send cancellation notification to all drivers (to remove ride from their UI)
                await _notification.NotifyAllDriversRideCanceled(rideId);
                _logger.LogInformation($"✓ All drivers notified about ride #{rideId} cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify drivers or clean up keys");
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
                var currentHash = await _redis.HashGetAllAsync(hKey);
                var driverData = currentHash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                // Get last known location to preserve availability
                var lastLat = driverData.GetValueOrDefault("lat") ?? "0";
                var lastLng = driverData.GetValueOrDefault("lng") ?? "0";
                var vehicleType = driverData.GetValueOrDefault("vehicleType") ?? "standard";

                var now = DateTime.UtcNow;
                await _redis.HashSetAsync(hKey, new HashEntry[]
                {
                    new("status", "Free"),
                    new("updatedAt", now.ToString("O")),
                    new("lat", lastLat),
                    new("lng", lastLng),
                    new("vehicleType", vehicleType)
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
                        DateTime.UtcNow,
                        "Baku");

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

        #region Trip History

        public async Task<PagedResult<UserTripHistoryItem>> GetUserTripHistoryAsync(int userId, TripHistoryQuery query, CancellationToken ct = default)
        {
            var pageNumber = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize);

            var ridesQuery = _db.Rides
                .AsNoTracking()
                .Include(r => r.Driver)
                    .ThenInclude(d => d!.User)
                .Include(r => r.Driver)
                    .ThenInclude(d => d!.Vehicle)
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .Where(r => r.UserId == userId);

            var totalCount = await ridesQuery.CountAsync(ct);

            var rides = await ridesQuery
                .OrderByDescending(r => r.RequestedTime
                    ?? r.CompletedTime
                    ?? r.CanceledTime
                    ?? r.PickUpTime
                    ?? r.AcceptedTime
                    ?? DateTime.MinValue)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = rides.Select(r => new UserTripHistoryItem
            {
                RideId = r.Id,
                DriverName = r.Driver?.User?.Name,
                DriverSurname = r.Driver?.User?.Surname,
                DriverPhoneNumber = r.Driver?.User?.PhoneNumber,
                DriverEmail = r.Driver?.User?.Email,
                VehiclePlateNumber = r.Driver?.Vehicle?.PlateNumber,
                VehicleType = r.Driver?.Vehicle?.VehicleType.ToString(),
                VehicleBrand = r.Driver?.Vehicle?.Brand,
                VehicleModel = r.Driver?.Vehicle?.Model,
                PickupAddress = FormatAddress(r.StartLocation),
                DestinationAddress = FormatAddress(r.EndLocation),
                Fare = r.EstimatedFare ?? r.ActualFare ?? 0,
                DiscountAmount = r.DiscountAmount,
                FinalFare = r.ActualFare ?? (r.DiscountAmount.HasValue
                    ? Math.Max(0, (r.EstimatedFare ?? 0) - r.DiscountAmount.Value)
                    : r.EstimatedFare),
                RideDateUtc = ResolveRideDateUtc(r),
                Status = r.Status.ToString()
            }).ToList();

            return new PagedResult<UserTripHistoryItem>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<PagedResult<DriverTripHistoryItem>> GetDriverTripHistoryAsync(int driverId, TripHistoryQuery query, CancellationToken ct = default)
        {
            var pageNumber = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize);

            var ridesQuery = _db.Rides
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .Where(r => r.DriverId == driverId);

            var totalCount = await ridesQuery.CountAsync(ct);

            var rides = await ridesQuery
                .OrderByDescending(r => r.RequestedTime
                    ?? r.CompletedTime
                    ?? r.CanceledTime
                    ?? r.PickUpTime
                    ?? r.AcceptedTime
                    ?? DateTime.MinValue)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = rides.Select(r => new DriverTripHistoryItem
            {
                RideId = r.Id,
                UserName = r.User?.Name,
                UserSurname = r.User?.Surname,
                UserPhoneNumber = r.User?.PhoneNumber,
                UserEmail = r.User?.Email,
                PickupAddress = FormatAddress(r.StartLocation),
                DestinationAddress = FormatAddress(r.EndLocation),
                RideDateUtc = ResolveRideDateUtc(r),
                Status = r.Status.ToString()
            }).ToList();

            return new PagedResult<DriverTripHistoryItem>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        #endregion
    }
}
