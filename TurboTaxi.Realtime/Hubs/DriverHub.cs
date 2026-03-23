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

        public Task SubscribeDriver(string driverId)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"driver:{driverId}");

        public Task UnsubscribeDriver(string driverId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver:{driverId}");

        public Task SubscribeUser(string userId)
            => Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        public Task UnsubscribeUser(string userId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        /// <summary>
        /// Real-time driver location update over SignalR.
        /// This is equivalent to calling the REST endpoint /api/driver/location.
        /// </summary>
        public async Task UpdateLocation(DriverLocationUpdateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.DriverId <= 0)
            {
                throw new HubException("DriverId must be greater than 0.");
            }

            _logger.LogInformation(
                "[SignalR] Driver #{DriverId} location update via hub: ({Lat}, {Lng})",
                request.DriverId,
                request.Latitude,
                request.Longitude);

            await _driverLocationService.UpdateAsync(request, Context.ConnectionAborted);
        }
    }
}
