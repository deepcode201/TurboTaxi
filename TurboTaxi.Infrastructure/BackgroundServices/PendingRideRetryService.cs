using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.Infrastructure.BackgroundServices
{
    public class PendingRideRetryService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PendingRideRetryService> _logger;

        public PendingRideRetryService(
            IServiceProvider serviceProvider,
            ILogger<PendingRideRetryService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PendingRideRetryService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RetryPendingRidesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PendingRideRetryService");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task RetryPendingRidesAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var redis = scope.ServiceProvider.GetRequiredService<IRedisService>();
            var notification = scope.ServiceProvider.GetRequiredService<IRideNotificationService>();

            // Get pending rides older than 10 seconds
            var cutoffTime = DateTime.UtcNow.AddSeconds(-10);
            var pendingRides = await db.Rides
                .Include(r => r.StartLocation)
                .Include(r => r.EndLocation)
                .Where(r => r.Status == RideStatus.Pending && r.RequestedTime < cutoffTime)
                .ToListAsync(ct);

            if (!pendingRides.Any())
                return;

            _logger.LogInformation($"Found {pendingRides.Count} pending rides to retry");

            foreach (var ride in pendingRides)
            {
                try
                {
                    var vehicleType = ride.RequestedVehicleType.ToString().ToLowerInvariant();
                    var geoKey = $"geo:drivers:{vehicleType}";
                    
                    var nearbyDrivers = await redis.GeoRadiusAsync(
                        geoKey, 
                        ride.StartLocation.Longitude, 
                        ride.StartLocation.Latitude, 
                        3.0);

                    var candidateIds = new List<int>();
                    var driverCutoff = DateTime.UtcNow.AddSeconds(-60);

                    foreach (var result in nearbyDrivers)
                    {
                        var driverId = int.Parse(result.Member);
                        var hashKey = RedisKeys.DriverHash(driverId);
                        var hash = await redis.HashGetAllAsync(hashKey);
                        var dict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                        if (dict.TryGetValue("status", out var status) && status == "Free" &&
                            dict.TryGetValue("updatedAt", out var updatedStr) &&
                            DateTime.TryParse(updatedStr, out var updated) && updated > driverCutoff)
                        {
                            candidateIds.Add(driverId);
                        }
                    }

                    if (candidateIds.Any())
                    {
                        _logger.LogInformation($"Notifying {candidateIds.Count} new drivers for ride #{ride.Id}");
                        
                        // Notify drivers about the ride
                        await notification.NotifyNearestDriversAsync(candidateIds, new
                        {
                            RideId = ride.Id,
                            PickupLat = ride.StartLocation.Latitude,
                            PickupLng = ride.StartLocation.Longitude,
                            DestinationLat = ride.EndLocation?.Latitude,
                            DestinationLng = ride.EndLocation?.Longitude
                        });

                        // Notify user that drivers are being notified
                        await notification.NotifyUserAsync(ride.UserId, new
                        {
                            RideId = ride.Id,
                            Status = "Searching",
                            Message = $"Notifying {candidateIds.Count} nearby drivers",
                            DriverCount = candidateIds.Count
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error retrying ride #{ride.Id}");
                }
            }
        }
    }
}
