using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TurboTaxi.Realtime.Hubs;

namespace TurboTaxi.Realtime.Services
{
    public interface IMessageQueueService
    {
        Task<bool> EnqueueAndSendAsync(string hubGroup, string methodName, params object[] args);
        Task<int> GetQueueCountAsync();
        Task ClearQueueAsync();
    }

    public class MessageQueueService : IMessageQueueService
    {
        private readonly IHubContext<DriverHub, IDriverClient> _driverHub;
        private readonly IHubContext<DriverHub> _driverHubRaw;
        private readonly IHubContext<UserHub, IUserClient> _userHub;
        private readonly IHubContext<UserHub> _userHubRaw;
        private readonly ILogger<MessageQueueService> _logger;

        private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new();
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 500;

        public MessageQueueService(
            IHubContext<DriverHub, IDriverClient> driverHub,
            IHubContext<DriverHub> driverHubRaw,
            IHubContext<UserHub, IUserClient> userHub,
            IHubContext<UserHub> userHubRaw,
            ILogger<MessageQueueService> logger)
        {
            _driverHub = driverHub;
            _driverHubRaw = driverHubRaw;
            _userHub = userHub;
            _userHubRaw = userHubRaw;
            _logger = logger;
        }

        public async Task<bool> EnqueueAndSendAsync(string hubGroup, string methodName, params object[] args)
        {
            var messageId = Guid.NewGuid().ToString();
            var message = new QueuedMessage
            {
                Id = messageId,
                HubGroup = hubGroup,
                MethodName = methodName,
                Args = args,
                EnqueuedAt = DateTime.UtcNow,
                Attempts = 0
            };

            try
            {
                var sent = await TrySendAsync(message);

                if (!sent)
                {
                    _messageQueue.Enqueue(message);
                    _logger.LogWarning("[Queue] Message {MessageId} queued for retry (group: {Group}, method: {Method})",
                        messageId, hubGroup, methodName);
                    _ = RetryQueuedMessagesAsync();
                }

                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Queue] Error enqueuing message {MessageId}", messageId);
                _messageQueue.Enqueue(message);
                _ = RetryQueuedMessagesAsync();
                return false;
            }
        }

        private async Task<bool> TrySendAsync(QueuedMessage message)
        {
            try
            {
                message.Attempts++;
                var sent = await DispatchAsync(message);

                if (!sent)
                {
                    _logger.LogWarning("[Queue] Message {MessageId} not dispatched (group: {Group})",
                        message.Id, message.HubGroup);
                }

                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Queue] Failed to send message {MessageId} (attempt {Attempt})",
                    message.Id, message.Attempts);
                return false;
            }
        }

        private async Task<bool> DispatchAsync(QueuedMessage message)
        {
            if (string.Equals(message.MethodName, "NewRideRequest", StringComparison.OrdinalIgnoreCase)
                && message.Args.Length >= 2)
            {
                var requestId = message.Args[0]?.ToString() ?? string.Empty;
                var payload = message.Args[1];

                if (message.HubGroup == "all_drivers")
                {
                    await _driverHub.Clients.Group("all_drivers").NewRideRequest(requestId, payload);
                    return true;
                }

                if (message.HubGroup.StartsWith("driver:", StringComparison.OrdinalIgnoreCase))
                {
                    await _driverHub.Clients.Group(message.HubGroup).NewRideRequest(requestId, payload);
                    return true;
                }
            }

            if (string.Equals(message.MethodName, "RideRequestRemoved", StringComparison.OrdinalIgnoreCase)
                && message.Args.Length >= 2)
            {
                var rideId = Convert.ToInt32(message.Args[0]);
                var payload = message.Args[1];

                if (message.HubGroup == "all_drivers"
                    || message.HubGroup.StartsWith("driver:", StringComparison.OrdinalIgnoreCase))
                {
                    var target = message.HubGroup == "all_drivers"
                        ? _driverHub.Clients.Group("all_drivers")
                        : _driverHub.Clients.Group(message.HubGroup);
                    await target.RideRequestRemoved(rideId, payload);
                    return true;
                }
            }

            if (string.Equals(message.MethodName, "RideStatusUpdated", StringComparison.OrdinalIgnoreCase)
                && message.Args.Length >= 3)
            {
                var rideId = Convert.ToInt32(message.Args[0]);
                var status = message.Args[1]?.ToString() ?? string.Empty;
                var payload = message.Args[2];

                if (message.HubGroup == "all_drivers")
                {
                    await _driverHub.Clients.Group("all_drivers").RideStatusUpdated(rideId, status, payload);
                    return true;
                }

                if (message.HubGroup.StartsWith("driver:", StringComparison.OrdinalIgnoreCase))
                {
                    await _driverHub.Clients.Group(message.HubGroup).RideStatusUpdated(rideId, status, payload);
                    return true;
                }

                if (message.HubGroup.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
                {
                    await _userHub.Clients.Group(message.HubGroup).RideStatusUpdated(rideId, status, payload);
                    return true;
                }
            }

            if (message.HubGroup.StartsWith("driver:", StringComparison.OrdinalIgnoreCase))
            {
                await _driverHubRaw.Clients.Group(message.HubGroup).SendCoreAsync(message.MethodName, message.Args);
                return true;
            }

            if (message.HubGroup.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
            {
                await _userHubRaw.Clients.Group(message.HubGroup).SendCoreAsync(message.MethodName, message.Args);
                return true;
            }

            return false;
        }

        private async Task RetryQueuedMessagesAsync()
        {
            while (_messageQueue.TryPeek(out var message))
            {
                if (message.Attempts >= MaxRetries)
                {
                    _messageQueue.TryDequeue(out _);
                    _logger.LogError("[Queue] Message {MessageId} dropped after {MaxRetries} attempts",
                        message.Id, MaxRetries);
                    continue;
                }

                await Task.Delay(RetryDelayMs * message.Attempts);

                var sent = await TrySendAsync(message);
                if (sent)
                {
                    _messageQueue.TryDequeue(out _);
                }
                else
                {
                    break;
                }
            }
        }

        public Task<int> GetQueueCountAsync() => Task.FromResult(_messageQueue.Count);

        public Task ClearQueueAsync()
        {
            _messageQueue.Clear();
            _logger.LogInformation("[Queue] Message queue cleared");
            return Task.CompletedTask;
        }

        private class QueuedMessage
        {
            public string Id { get; set; } = string.Empty;
            public string HubGroup { get; set; } = string.Empty;
            public string MethodName { get; set; } = string.Empty;
            public object[] Args { get; set; } = Array.Empty<object>();
            public DateTime EnqueuedAt { get; set; }
            public int Attempts { get; set; }
        }
    }
}
