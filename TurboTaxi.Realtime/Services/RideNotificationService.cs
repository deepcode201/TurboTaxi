using Microsoft.AspNetCore.SignalR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Realtime.Hubs;

namespace TurboTaxi.Realtime.Services
{
    public class RideNotificationService : IRideNotificationService
    {
        private readonly IHubContext<DriverHub, IDriverClient> _driverHub;

        public RideNotificationService(IHubContext<DriverHub, IDriverClient> driverHub)
        {
            _driverHub = driverHub;
        }

        private static int ExtractRideId(object payload)
        {
            try
            {
                var type = payload.GetType();
                var prop = type.GetProperty("RideId") ?? type.GetProperty("rideId");
                if (prop != null)
                {
                    var val = prop.GetValue(payload);
                    if (val is int i) return i;
                    if (val is string s && int.TryParse(s, out var si)) return si;
                }
            }
            catch { }
            return 0;
        }

        public Task NotifyDriverAsync(int driverId, object payload)
        {
            var rideId = ExtractRideId(payload);
            return _driverHub.Clients.Group($"driver:{driverId}").RideStatusUpdated(rideId, "notification", payload);
        }

        public Task NotifyUserAsync(int userId, object payload)
        {
            var rideId = ExtractRideId(payload);
            return _driverHub.Clients.Group($"user:{userId}").RideStatusUpdated(rideId, "ride_update", payload);
        }

        public async Task NotifyNearestDriversAsync(List<int> driverIds, object payload)
        {
            foreach (var driverId in driverIds)
            {
                await _driverHub.Clients.Group($"driver:{driverId}").NewRideRequest("new_ride", payload);
            }
        }

        public async Task NotifyAllDriversRideCanceled(int rideId)
        {
            // Broadcast to all connected drivers that this ride was canceled
            await _driverHub.Clients.All.RideStatusUpdated(rideId, "RideCanceled", new
            {
                RideId = rideId,
                Status = "Canceled",
                Message = "This ride has been canceled by the user"
            });
        }

        public async Task NotifyOtherDriversRideTaken(int rideId, int acceptedDriverId)
        {
            // Broadcast to ALL connected clients (drivers) that this ride is no longer available
            // We use Clients.All and let client-side filter, or we can use AllExcept
            // Since SignalR doesn't have GroupExcept with single parameter, we broadcast to All
            await _driverHub.Clients.All.RideStatusUpdated(rideId, "RideTaken", new
            {
                RideId = rideId,
                Status = "Taken",
                AcceptedByDriverId = acceptedDriverId,
                Message = "This ride was accepted by another driver"
            });
        }
    }
}
