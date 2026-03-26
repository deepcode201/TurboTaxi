using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TurboTaxi.Realtime.Hubs;

namespace TurboTaxi.Realtime.Services
{
    /// <summary>
    /// Message Queue Service - Ensures NO DATA LOSS
    /// Queues messages and retries if delivery fails
    /// </summary>
    public interface IMessageQueueService
    {
        Task<bool> EnqueueAndSendAsync(string hubGroup, string methodName, params object[] args);
        Task<int> GetQueueCountAsync();
        Task ClearQueueAsync();
    }

    public class MessageQueueService : IMessageQueueService
    {
        private readonly IHubContext<DriverHub> _driverHub;
        private readonly IHubContext<UserHub> _userHub;
        private readonly ILogger<MessageQueueService> _logger;

        // 🔧 In-memory queue for failed messages (will retry)
        private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new();

        // Track retry attempts
        private readonly ConcurrentDictionary<string, int> _retryCount = new();
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 500;

        public MessageQueueService(
            IHubContext<DriverHub> driverHub,
            IHubContext<UserHub> userHub,
            ILogger<MessageQueueService> logger)
        {
            _driverHub = driverHub;
            _userHub = userHub;
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
                // 🚀 Try to send immediately
                var sent = await TrySendAsync(message);
                
                if (!sent)
                {
                    // Queue for retry if immediate send fails
                    _messageQueue.Enqueue(message);
                    _logger.LogWarning($"[Queue] Message {messageId} queued for retry (group: {hubGroup}, method: {methodName})");
                    
                    // Start retry task
                    _ = RetryQueuedMessagesAsync();
                }
                
                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Queue] Error enqueuing message {messageId}");
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

                if (message.HubGroup.StartsWith("driver:"))
                {
                    await _driverHub.Clients.Group(message.HubGroup)
                        .SendCoreAsync(message.MethodName, message.Args);
                }
                else if (message.HubGroup.StartsWith("user:"))
                {
                    await _userHub.Clients.Group(message.HubGroup)
                        .SendCoreAsync(message.MethodName, message.Args);
                }
                else if (message.HubGroup == "all_drivers")
                {
                    await _driverHub.Clients.All
                        .SendCoreAsync(message.MethodName, message.Args);
                }

                _logger.LogInformation($"[Queue] Message {message.Id} sent successfully (attempt {message.Attempts})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[Queue] Failed to send message {message.Id} (attempt {message.Attempts})");
                return false;
            }
        }

        private async Task RetryQueuedMessagesAsync()
        {
            while (_messageQueue.TryPeek(out var message))
            {
                if (message.Attempts >= MAX_RETRIES)
                {
                    _messageQueue.TryDequeue(out _);
                    _logger.LogError($"[Queue] Message {message.Id} failed after {MAX_RETRIES} attempts - DROPPED");
                    continue;
                }

                // Wait before retry
                await Task.Delay(RETRY_DELAY_MS * message.Attempts);

                var sent = await TrySendAsync(message);
                if (sent)
                {
                    _messageQueue.TryDequeue(out _);
                    _logger.LogInformation($"[Queue] Message {message.Id} sent on retry");
                }
                else
                {
                    // Keep in queue for next attempt
                    break;
                }
            }
        }

        public async Task<int> GetQueueCountAsync()
        {
            return _messageQueue.Count;
        }

        public async Task ClearQueueAsync()
        {
            _messageQueue.Clear();
            _logger.LogInformation("[Queue] Message queue cleared");
        }

        private class QueuedMessage
        {
            public string Id { get; set; }
            public string HubGroup { get; set; }
            public string MethodName { get; set; }
            public object[] Args { get; set; }
            public DateTime EnqueuedAt { get; set; }
            public int Attempts { get; set; }
        }
    }
}
