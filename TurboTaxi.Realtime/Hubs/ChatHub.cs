using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Chat;

namespace TurboTaxi.Realtime.Hubs
{
    public interface IChatClient
    {
        /// <summary>Pushed after a message is stored (same shape as REST item).</summary>
        Task ReceiveChatMessage(ChatMessageDto message);
    }

    /// <summary>Live ride chat. Connect to <c>/hubs/chat</c>, then call <see cref="JoinRideChat"/>.</summary>
    public sealed class ChatHub : Hub<IChatClient>
    {
        public const string HubPath = "/hubs/chat";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IServiceScopeFactory scopeFactory, ILogger<ChatHub> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public static string GroupForRide(int rideId) => $"ride_chat:{rideId}";

        /// <param name="actorRole"><c>user</c> (passenger) or <c>driver</c>.</param>
        /// <param name="actorId">Passenger: User.Id. Driver app: Driver.Id.</param>
        public async Task JoinRideChat(int rideId, string actorRole, int actorId)
        {
            using var scope = _scopeFactory.CreateScope();
            var access = scope.ServiceProvider.GetRequiredService<IRideChatAccess>();
            if (!await access.CanAccessAsync(rideId, actorRole, actorId))
                throw new HubException("Not allowed to join this ride chat.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupForRide(rideId));
            _logger.LogInformation(
                "Chat join: ride {RideId}, role {Role}, actor {ActorId}, connection {ConnectionId}",
                rideId, actorRole, actorId, Context.ConnectionId);
        }

        public async Task LeaveRideChat(int rideId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForRide(rideId));
        }
    }
}
