using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.Infrastructure.Services
{
    public class PendingRideChecker : IPendingRideChecker
    {
        private readonly ApplicationDbContext _db;
        private readonly IRideNotificationService _notification;
        private readonly IMemoryCache _cache;
        private readonly IRedisService _redis;
        private readonly ILogger<PendingRideChecker> _logger;

        public PendingRideChecker(
            ApplicationDbContext db,
            IRideNotificationService notification,
            IMemoryCache cache,
            IRedisService redis,
            ILogger<PendingRideChecker> logger)
        {
            _db = db;
            _notification = notification;
            _cache = cache;
            _redis = redis;
            _logger = logger;
        }

        public async Task CheckNearbyPendingRidesAsync(int driverId, double latitude, double longitude, string vehicleType, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation($"Checking pending rides for driver #{driverId} at ({latitude}, {longitude})");

                var pendingRides = await _db.Rides
                    .Include(r => r.StartLocation)
                    .Include(r => r.EndLocation)
                    .Where(r => r.Status == RideStatus.Pending)
                    .ToListAsync(ct);

                if (pendingRides.Count == 0) return;

                int notifiedCount = 0;
                foreach (var ride in pendingRides)
                {
                    var distanceKm = CalculateDistance(latitude, longitude, ride.StartLocation.Latitude, ride.StartLocation.Longitude);
                    if (distanceKm > 5.0) continue;

                    var key = $"notify:ride:{ride.Id}:driver:{driverId}";
                    var firstTime = await _redis.SetStringIfNotExistsAsync(key, "1", TimeSpan.FromHours(6));
                    if (!firstTime) continue;

                    await _notification.NotifyNearestDriversAsync(new List<int> { driverId }, new
                    {
                        RideId = ride.Id,
                        PickupLat = ride.StartLocation.Latitude,
                        PickupLng = ride.StartLocation.Longitude,
                        DestinationLat = ride.EndLocation?.Latitude,
                        DestinationLng = ride.EndLocation?.Longitude
                    });

                    await _notification.NotifyUserAsync(ride.UserId, new
                    {
                        RideId = ride.Id,
                        Status = "Searching",
                        Message = $"Driver #{driverId} is {Math.Round(distanceKm, 1)}km away",
                        DriverId = driverId,
                        Distance = Math.Round(distanceKm, 2)
                    });

                    notifiedCount++;
                }

                _logger.LogInformation($"Sent {notifiedCount} notifications to driver #{driverId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking nearby pending rides for driver #{driverId}");
            }
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
