using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TurboTaxi.Realtime.Hubs
{
    /// <summary>
    /// Client interface for User (Rider) SignalR notifications.
    /// Defines all methods that can be called on the client side.
    /// </summary>
    public interface IUserClient
    {
        /// <summary>
        /// Notify user about a new ride request (when driver accepted or ride state changed).
        /// </summary>
        Task RideStatusUpdated(int rideId, string status, object payload);

        /// <summary>
        /// Notify user about driver location update in real-time.
        /// </summary>
        Task DriverLocationUpdated(int driverId, double latitude, double longitude);

        /// <summary>
        /// Generic notification for other ride-related events.
        /// </summary>
        Task NotifyRide(int rideId, string message, object data);

        /// <summary>
        /// Notify user of ride cancellation.
        /// </summary>
        Task RideCanceled(int rideId, string reason);
    }

    /// <summary>
    /// SignalR Hub for User (Rider) real-time communication.
    /// Clients connect via /hubs/user endpoint.
    /// </summary>
    public class UserHub : Hub<IUserClient>
    {
        private readonly ILogger<UserHub> _logger;

        public UserHub(ILogger<UserHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called when a user connects to the hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[SignalR] User connected: ConnectionId={connectionId}");
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a user disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[SignalR] User disconnected: ConnectionId={connectionId}");
            if (exception != null)
            {
                _logger.LogError(exception, $"[SignalR] User disconnection error: {exception.Message}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe user to receive updates for a specific ride.
        /// Joins group: user:{userId}
        /// </summary>
        public async Task SubscribeUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("UserId cannot be empty.");
            }

            var groupName = $"user:{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] User {userId} subscribed. ConnectionId={Context.ConnectionId}, Group={groupName}");
        }

        /// <summary>
        /// Unsubscribe user from receiving updates for a specific ride.
        /// </summary>
        public async Task UnsubscribeUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("UserId cannot be empty.");
            }

            var groupName = $"user:{userId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] User {userId} unsubscribed. ConnectionId={Context.ConnectionId}");
        }

        /// <summary>
        /// Subscribe to driver location updates.
        /// Joins group: driver_location:{driverId}
        /// </summary>
        public async Task SubscribeToDriverLocation(int driverId)
        {
            if (driverId <= 0)
            {
                throw new HubException("DriverId must be greater than 0.");
            }

            var groupName = $"driver_location:{driverId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] User subscribed to driver {driverId} location. Group={groupName}");
        }

        /// <summary>
        /// Unsubscribe from driver location updates.
        /// </summary>
        public async Task UnsubscribeFromDriverLocation(int driverId)
        {
            if (driverId <= 0)
            {
                throw new HubException("DriverId must be greater than 0.");
            }

            var groupName = $"driver_location:{driverId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"[SignalR] User unsubscribed from driver {driverId} location.");
        }

        /// <summary>
        /// Health check / ping method to keep connection alive.
        /// </summary>
        public Task Ping()
        {
            _logger.LogDebug($"[SignalR] Ping received from {Context.ConnectionId}");
            return Task.CompletedTask;
        }
    }
}
