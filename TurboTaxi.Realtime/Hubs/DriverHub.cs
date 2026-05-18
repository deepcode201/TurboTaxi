using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Helpers;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Realtime;
using TurboTaxi.Models.Rides;

namespace TurboTaxi.Realtime.Hubs
{
    public interface IDriverClient
    {
        Task DriverLocationUpdated(int driverId, double lat, double lng);
        Task NewRideRequest(string requestId, object payload);
        /// <summary>Remove pending ride card from driver UI (user canceled or another driver accepted).</summary>
        Task RideRequestRemoved(int rideId, object payload);
        Task RideStatusUpdated(int rideId, string status, object payload);
    }

    public class DriverHub : Hub<IDriverClient>
    {
        private readonly IDriverLocationService _driverLocationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DriverHub> _logger;

        public DriverHub(
            IDriverLocationService driverLocationService,
            IServiceProvider serviceProvider,
            ILogger<DriverHub> logger)
        {
            _driverLocationService = driverLocationService;
            _serviceProvider = serviceProvider;
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
        /// Accept a pending ride (same as POST /api/rides/{rideId}/accept).
        /// Location is optional — backend uses Redis, request coords, or pickup fallback.
        /// </summary>
        public Task<RideAcceptedResponse> AcceptRide(
            int rideId,
            int driverId,
            double? driverLat = null,
            double? driverLng = null)
            => AcceptRideCore(rideId.ToString(), driverId, driverLat, driverLng);

        /// <summary>Accept using SignalR requestId string (e.g. "ride_123").</summary>
        public Task<RideAcceptedResponse> AcceptRideByRequestId(
            string rideIdOrRequestId,
            int driverId,
            double? driverLat = null,
            double? driverLng = null)
            => AcceptRideCore(rideIdOrRequestId, driverId, driverLat, driverLng);

        private async Task<RideAcceptedResponse> AcceptRideCore(
            string rideIdOrRequestId,
            int driverId,
            double? driverLat,
            double? driverLng)
        {
            var rideId = RideIdParser.Parse(rideIdOrRequestId);

            _logger.LogInformation(
                "[SignalR] AcceptRide: rideId={RideId}, driverId={DriverId}, lat={Lat}, lng={Lng}",
                rideId, driverId, driverLat, driverLng);

            using var scope = _serviceProvider.CreateScope();
            var rideService = scope.ServiceProvider.GetRequiredService<IRideService>();

            try
            {
                var result = await rideService.AcceptRideAsync(
                    rideId,
                    new AcceptRideRequest
                    {
                        DriverId = driverId,
                        DriverLat = driverLat,
                        DriverLng = driverLng
                    },
                    Context.ConnectionAborted);

                var driverGroupName = $"driver:{driverId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, driverGroupName);

                return result;
            }
            catch (KeyNotFoundException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        /// <summary>
        /// Legacy hub method name — now performs real accept (not subscribe-only).
        /// </summary>
        public Task<RideAcceptedResponse> OnRideAccepted(
            int driverId,
            int rideId,
            double? driverLat = null,
            double? driverLng = null)
            => AcceptRideCore(rideId.ToString(), driverId, driverLat, driverLng);
    }
}
