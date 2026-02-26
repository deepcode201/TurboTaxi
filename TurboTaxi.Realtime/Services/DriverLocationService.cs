using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Realtime;
using TurboTaxi.Realtime.Hubs;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.Realtime.Services
{
    public class DriverLocationService : IDriverLocationService
    {
        private readonly IRedisService _redis;
        private readonly IHubContext<DriverHub, IDriverClient> _driverHub;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DriverLocationService> _logger;

        public DriverLocationService(
            IRedisService redis,
            IHubContext<DriverHub, IDriverClient> driverHub,
            IServiceProvider serviceProvider,
            ILogger<DriverLocationService> logger)
        {
            _redis = redis;
            _driverHub = driverHub;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task UpdateAsync(DriverLocationUpdateRequest request, CancellationToken ct = default)
        {
            var updatedAt = DateTime.UtcNow;
            _logger.LogInformation($"?? Driver #{request.DriverId} location update: ({request.Latitude}, {request.Longitude})");

            // Update Redis with driver location
            var hashKey = RedisKeys.DriverHash(request.DriverId);
            var entries = new HashEntry[]
            {
                new("lat", request.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("lng", request.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("status", request.DriverStatus ?? "Free"),
                new("vehicleType", request.VehicleType ?? "Standard"),
                new("updatedAt", updatedAt.ToString("o"))
            };
            await _redis.HashSetAsync(hashKey, entries);

            // Update geo-spatial index
            var vt = (request.VehicleType ?? "Standard").ToLowerInvariant();
            var geoKey = $"geo:drivers:{vt}";
            await _redis.GeoAddAsync(geoKey, request.Longitude, request.Latitude, member: request.DriverId.ToString());

            // Update heartbeat
            var heartbeatKey = RedisKeys.DriverHeartbeat(request.DriverId);
            await _redis.SetStringIfNotExistsAsync(heartbeatKey, "1", TimeSpan.FromSeconds(10));
            await _redis.SetExpiryAsync(heartbeatKey, TimeSpan.FromSeconds(10));

            // ?? BROADCAST DRIVER LOCATION TO DRIVER GROUP
            await _driverHub.Clients.Group($"driver:{request.DriverId}")
                .DriverLocationUpdated(request.DriverId, request.Latitude, request.Longitude);

            // ?? CHECK FOR ACTIVE RIDE AND BROADCAST TO USER
            await BroadcastLocationToActiveRideUserAsync(request.DriverId, request.Latitude, request.Longitude, ct);

            using var scope = _serviceProvider.CreateScope();
            
            // REAL-TIME: Check for nearby pending rides
            var pendingRideChecker = scope.ServiceProvider.GetRequiredService<IPendingRideChecker>();
            try
            {
                await pendingRideChecker.CheckNearbyPendingRidesAsync(
                    request.DriverId,
                    request.Latitude,
                    request.Longitude,
                    request.VehicleType ?? "Standard",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in pending ride check for driver #{request.DriverId}");
            }

            // OFF-ROUTE DETECTION: Check if driver deviated from planned route
            var offRouteDetection = scope.ServiceProvider.GetRequiredService<IOffRouteDetectionService>();
            try
            {
                var routeRecalculated = await offRouteDetection.CheckAndRecalculateIfOffRouteAsync(
                    request.DriverId,
                    request.Latitude,
                    request.Longitude,
                    ct);

                if (routeRecalculated)
                {
                    _logger.LogInformation($"?? Route recalculated for driver #{request.DriverId} (off-route detected)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in off-route detection for driver #{request.DriverId}");
            }
        }

        /// <summary>
        /// ?? Broadcast driver location to user if driver has an active ride.
        /// We read currentUserId directly from Redis hash for fast lookup (no DB query needed).
        /// </summary>
        private async Task BroadcastLocationToActiveRideUserAsync(int driverId, double lat, double lng, CancellationToken ct)
        {
            try
            {
                // Check if driver has an active ride in Redis
                var hashKey = RedisKeys.DriverHash(driverId);
                var hash = await _redis.HashGetAllAsync(hashKey);
                var dict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                // ? Get userId directly from Redis (stored when ride is accepted)
                if (dict.TryGetValue("currentUserId", out var userIdStr) && int.TryParse(userIdStr, out var userId))
                {
                    _logger.LogDebug($"?? Broadcasting driver #{driverId} location to user #{userId}");
                    
                    // Broadcast to user group
                    await _driverHub.Clients.Group($"user:{userId}")
                        .DriverLocationUpdated(driverId, lat, lng);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error broadcasting location to user for driver #{driverId}");
            }
        }
    }
}
