using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Realtime;

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
        private readonly IDriverLocationService _driverLocationService;
        private readonly ILogger<DriverHub> _logger;

        public DriverHub(IDriverLocationService driverLocationService, ILogger<DriverHub> logger)
        {
            _driverLocationService = driverLocationService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"[SignalR] Driver connected: {Context.ConnectionId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, "all_drivers");
            _logger.LogInformation("[SignalR] Connection {ConnectionId} joined all_drivers", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"[SignalR] Driver disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public Task SubscribeDriver(string driverId)
        {
            _logger.LogInformation($"[SignalR] Driver #{driverId} subscribing to group driver:{driverId}");
            return Groups.AddToGroupAsync(Context.ConnectionId, $"driver:{driverId}");
        }

        public Task UnsubscribeDriver(string driverId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver:{driverId}");

        public Task SubscribeUser(string userId)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        public Task UnsubscribeUser(string userId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        /// <summary>
        /// Real-time driver location update over SignalR.
        /// This is equivalent to calling the REST endpoint /api/driver/location.
        /// 🔧 AUTO-SUBSCRIBE: Driver automatically subscribes to their own group on first update.
        /// </summary>
        public async Task UpdateLocation(DriverLocationUpdateRequest request)
        {
            if (request == null)
            {
                _logger.LogError("[SignalR] UpdateLocation received NULL request!");
                throw new ArgumentNullException(nameof(request));
            }

            if (request.DriverId <= 0)
            {
                _logger.LogError("[SignalR] UpdateLocation received invalid DriverId: {DriverId}", request.DriverId);
                throw new HubException("DriverId must be greater than 0.");
            }

            _logger.LogInformation(
                "[SignalR] UpdateLocation RECEIVED: DriverId={DriverId}, Latitude={Latitude}, Longitude={Longitude}, VehicleType={VehicleType}, DriverStatus={DriverStatus}",
                request.DriverId,
                request.Latitude,
                request.Longitude,
                request.VehicleType,
                request.DriverStatus);

            // 🔴 CRITICAL CHECK: Are coordinates 0?
            if (request.Latitude == 0 && request.Longitude == 0)
            {
                _logger.LogError(
                    "[SignalR] ❌ CRITICAL: Driver #{DriverId} sent coordinates (0, 0) - Location mapping is BROKEN!",
                    request.DriverId);
            }

            // 🔧 AUTO-SUBSCRIBE: Ensure driver is subscribed to their own group
            // This guarantees that NewRideRequest events will reach the driver
            try
            {
                var driverGroupName = $"driver:{request.DriverId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, driverGroupName);
                _logger.LogInformation($"[SignalR] Driver #{request.DriverId} auto-subscribed to group '{driverGroupName}'");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Failed to auto-subscribe driver #{request.DriverId}");
            }

            await _driverLocationService.UpdateAsync(request, Context.ConnectionAborted);
        }

        /// <summary>
        /// Explicit driver acceptance of a ride - ensure driver is subscribed
        /// </summary>
        public async Task OnRideAccepted(int driverId, int rideId)
        {
            _logger.LogInformation($"[SignalR] Driver #{driverId} accepted ride #{rideId}");

            try
            {
                var driverGroupName = $"driver:{driverId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, driverGroupName);
                _logger.LogInformation($"[SignalR] Driver #{driverId} subscribed to group '{driverGroupName}' after accepting ride");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Failed to subscribe driver #{driverId} after ride acceptance");
            }
        }
    }
}
