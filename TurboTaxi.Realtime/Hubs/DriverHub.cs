using Microsoft.AspNetCore.SignalR;

namespace TurboTaxi.Realtime.Hubs
{
    public interface IDriverClient
    {
        Task DriverLocationUpdated(int driverId, double lat, double lng);
        Task NewRideRequest(string requestId, object payload);
        Task RideStatusUpdated(int rideId, string status, object payload);
    }

    public class DriverHub : Hub<IDriverClient>
    {
        public Task SubscribeDriver(string driverId)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"driver:{driverId}");

        public Task UnsubscribeDriver(string driverId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver:{driverId}");

        public Task SubscribeUser(string userId)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        public Task UnsubscribeUser(string userId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
    }
}
