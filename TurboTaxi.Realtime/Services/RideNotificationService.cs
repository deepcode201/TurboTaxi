using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Realtime.Services
{
    public class RideNotificationService : IRideNotificationService
    {
        private readonly IMessageQueueService _messageQueueService;
        private readonly ILogger<RideNotificationService> _logger;

        public RideNotificationService(
            IMessageQueueService messageQueueService,
            ILogger<RideNotificationService> logger)
        {
            _messageQueueService = messageQueueService;
            _logger = logger;
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

        /// <summary>
        /// Notify a specific driver about ride status update.
        /// </summary>
        public async Task NotifyDriverAsync(int driverId, object payload)
        {
            if (driverId <= 0)
            {
                _logger.LogWarning($"[SignalR] Invalid driverId: {driverId}");
                return;
            }

            try
            {
                var rideId = ExtractRideId(payload);
                var groupName = $"driver:{driverId}";

                _logger.LogInformation($"[SignalR] Notifying driver {driverId} (group: {groupName}) - RideId: {rideId}");
                await _messageQueueService.EnqueueAndSendAsync(groupName, "RideStatusUpdated", rideId, "notification", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Error notifying driver {driverId}");
            }
        }

        /// <summary>
        /// Notify a specific user (rider) about ride status update.
        /// </summary>
        public async Task NotifyUserAsync(int userId, object payload)
        {
            if (userId <= 0)
            {
                _logger.LogWarning($"[SignalR] Invalid userId: {userId}");
                return;
            }

            try
            {
                var rideId = ExtractRideId(payload);
                var groupName = $"user:{userId}";

                _logger.LogInformation($"[SignalR] Notifying user {userId} (group: {groupName}) - RideId: {rideId}");
                await _messageQueueService.EnqueueAndSendAsync(groupName, "RideStatusUpdated", rideId, "ride_update", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Error notifying user {userId}");
            }
        }

        /// <summary>
        /// Send new ride request notification to multiple drivers.
        /// </summary>
        public async Task NotifyNearestDriversAsync(List<int> driverIds, object payload)
        {
            if (driverIds == null || driverIds.Count == 0)
            {
                _logger.LogWarning("[SignalR] No driver IDs provided for notification");
                return;
            }

            try
            {
                var rideId = ExtractRideId(payload);
                _logger.LogInformation($"[SignalR] Sending new ride request {rideId} to {driverIds.Count} drivers");

                foreach (var driverId in driverIds)
                {
                    if (driverId <= 0) continue;

                    var groupName = $"driver:{driverId}";
                    await _messageQueueService.EnqueueAndSendAsync(groupName, "NewRideRequest", $"ride_{rideId}", payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Error notifying nearest drivers");
            }
        }

        /// <summary>
        /// Broadcast ride cancellation to all connected drivers.
        /// </summary>
        public async Task NotifyAllDriversRideCanceled(int rideId)
        {
            try
            {
                _logger.LogInformation($"[SignalR] Broadcasting ride {rideId} cancellation to all drivers");

                await _messageQueueService.EnqueueAndSendAsync("all_drivers", "RideStatusUpdated", rideId, "RideCanceled", new
                {
                    RideId = rideId,
                    Status = "Canceled",
                    Message = "This ride has been canceled by the user"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Error notifying drivers about ride {rideId} cancellation");
            }
        }

        /// <summary>
        /// Broadcast to all drivers that a ride has been accepted and is no longer available.
        /// </summary>
        public async Task NotifyOtherDriversRideTaken(int rideId, int acceptedDriverId)
        {
            try
            {
                _logger.LogInformation($"[SignalR] Broadcasting ride {rideId} taken by driver {acceptedDriverId}");

                await _messageQueueService.EnqueueAndSendAsync("all_drivers", "RideStatusUpdated", rideId, "RideTaken", new
                {
                    RideId = rideId,
                    Status = "Taken",
                    AcceptedByDriverId = acceptedDriverId,
                    Message = "This ride was accepted by another driver"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SignalR] Error notifying drivers about ride {rideId} being taken");
            }
        }
    }
}
