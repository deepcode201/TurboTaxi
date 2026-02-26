using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Infrastructure.Helpers;
using TurboTaxi.Models.Routes;

namespace TurboTaxi.Infrastructure.Services
{
    public class OffRouteDetectionService : IOffRouteDetectionService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRouteEstimationService _routeEstimation;
        private readonly IRideNotificationService _notification;
        private readonly ILogger<OffRouteDetectionService> _logger;

        // ? OFF-ROUTE DETECTION THRESHOLDS
        private const double OFF_ROUTE_THRESHOLD_METERS = 150.0; // Driver is 150m+ away from route
        private const bool ENABLE_OFF_ROUTE_DETECTION = true;

        public OffRouteDetectionService(
            ApplicationDbContext db,
            IRouteEstimationService routeEstimation,
            IRideNotificationService notification,
            ILogger<OffRouteDetectionService> logger)
        {
            _db = db;
            _routeEstimation = routeEstimation;
            _notification = notification;
            _logger = logger;
        }

        public async Task<bool> CheckAndRecalculateIfOffRouteAsync(
            int driverId,
            double currentLat,
            double currentLng,
            CancellationToken ct = default)
        {
            if (!ENABLE_OFF_ROUTE_DETECTION)
                return false;

            try
            {
                var ride = await _db.Rides
                    .Include(r => r.StartLocation)
                    .Include(r => r.EndLocation)
                    .FirstOrDefaultAsync(r =>
                        r.DriverId == driverId &&
                        (r.Status == RideStatus.Approved || r.Status == RideStatus.OnWay),
                        ct);

                if (ride == null)
                    return false;

                // ? Calculate initial route if missing
                if (string.IsNullOrWhiteSpace(ride.RoutePolyline))
                {
                    _logger.LogInformation($"No polyline for ride #{ride.Id}, calculating initial route...");
                    await CalculateAndStoreRouteAsync(ride, currentLat, currentLng, ct);
                    return true;
                }

                // ? Check if driver is off-route
                var polylineCoords = PolylineDistanceHelper.DecodePolyline(ride.RoutePolyline);

                if (polylineCoords.Count < 2)
                {
                    _logger.LogWarning($"Invalid polyline for ride #{ride.Id}, recalculating...");
                    await CalculateAndStoreRouteAsync(ride, currentLat, currentLng, ct);
                    return true;
                }

                var minDistance = PolylineDistanceHelper.MinDistanceToPolyline(
                    currentLat,
                    currentLng,
                    polylineCoords);

                _logger.LogDebug($"Ride #{ride.Id}: Driver distance to route = {minDistance:F1}m");

                if (minDistance > OFF_ROUTE_THRESHOLD_METERS)
                {
                    _logger.LogWarning($"?? Driver #{driverId} is OFF-ROUTE (distance: {minDistance:F1}m)");
                    _logger.LogInformation($"   Recalculating route from current location ? destination");
                    
                    await CalculateAndStoreRouteAsync(ride, currentLat, currentLng, ct);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in off-route detection for driver #{driverId}");
                return false;
            }
        }

        private async Task CalculateAndStoreRouteAsync(
            Domain.Entities.Ride ride,
            double driverLat,
            double driverLng,
            CancellationToken ct)
        {
            try
            {
                // ? Determine destination based on ride status
                double destLat, destLng;
                string routeType;
                
                if (ride.Status == RideStatus.Approved)
                {
                    // Driver is going to pickup user
                    destLat = ride.StartLocation.Latitude;
                    destLng = ride.StartLocation.Longitude;
                    routeType = "DRIVER?PICKUP";
                }
                else // OnWay
                {
                    // Driver is going to destination
                    destLat = ride.EndLocation.Latitude;
                    destLng = ride.EndLocation.Longitude;
                    routeType = "DRIVER?DESTINATION";
                }

                _logger.LogInformation($"?? Recalculating {routeType} route for ride #{ride.Id}");
                _logger.LogInformation($"   Current Driver: [{driverLat:F6}, {driverLng:F6}]");
                _logger.LogInformation($"   Target: [{destLat:F6}, {destLng:F6}]");

                var routeRequest = new RouteEstimateRequest
                {
                    Origin = new Coordinate { Latitude = driverLat, Longitude = driverLng },
                    Destination = new Coordinate { Latitude = destLat, Longitude = destLng },
                    VehicleType = ride.RequestedVehicleType.ToString()
                };

                var routeResponse = await _routeEstimation.EstimateAsync(routeRequest, ct);

                if (!routeResponse.Success || routeResponse.Data == null || string.IsNullOrWhiteSpace(routeResponse.Data.EncodedPolyline))
                {
                    _logger.LogError($"Failed to calculate route for ride #{ride.Id}: {routeResponse.Message}");
                    return;
                }

                ride.RoutePolyline = routeResponse.Data.EncodedPolyline;
                ride.EstimatedDistanceKm = routeResponse.Data.DistanceKm;
                ride.EstimatedDurationMinutes = routeResponse.Data.DurationMinutes;

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation($"? Ride #{ride.Id}: {routeType} route updated");
                _logger.LogInformation($"   Distance: {routeResponse.Data.DistanceKm}km");
                _logger.LogInformation($"   Duration: {routeResponse.Data.DurationMinutes}min");
                _logger.LogInformation($"   Polyline length: {routeResponse.Data.EncodedPolyline.Length} chars");

                // ? Broadcast updated route to user & driver
                await NotifyRouteUpdatedAsync(ride, routeResponse.Data, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating route for ride #{ride.Id}");
            }
        }

        private async Task NotifyRouteUpdatedAsync(
            Domain.Entities.Ride ride,
            RouteEstimateData routeData,
            CancellationToken ct)
        {
            try
            {
                // ? Send updated polyline WITHOUT updating marker positions
                // Frontend should keep pickup/destination markers in same place
                var payload = new
                {
                    RideId = ride.Id,
                    EncodedPolyline = routeData.EncodedPolyline,
                    DistanceKm = routeData.DistanceKm,
                    DurationMinutes = routeData.DurationMinutes,
                    Status = ride.Status.ToString(),
                    // ? Send BOTH pickup and destination for reference (markers won't move)
                    PickupLat = ride.StartLocation.Latitude,
                    PickupLng = ride.StartLocation.Longitude,
                    DestinationLat = ride.EndLocation.Latitude,
                    DestinationLng = ride.EndLocation.Longitude,
                    IsRouteUpdate = true  // ? Flag for frontend to handle differently
                };

                _logger.LogInformation($"?? Broadcasting route update: RideId={ride.Id}");
                _logger.LogInformation($"   Polyline: {routeData.EncodedPolyline.Length} chars");
                _logger.LogInformation($"   Pickup: [{ride.StartLocation.Latitude:F6}, {ride.StartLocation.Longitude:F6}]");
                _logger.LogInformation($"   Destination: [{ride.EndLocation.Latitude:F6}, {ride.EndLocation.Longitude:F6}]");
                _logger.LogInformation($"   Status: {ride.Status}");

                await _notification.NotifyUserAsync(ride.UserId, payload);

                if (ride.DriverId.HasValue)
                {
                    await _notification.NotifyDriverAsync(ride.DriverId.Value, payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying route update for ride #{ride.Id}");
            }
        }
    }
}
