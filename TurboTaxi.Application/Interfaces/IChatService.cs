using TurboTaxi.Models.Chat;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.Application.Interfaces
{
    public interface IChatService
    {
        Task<ChatMessageDto> SendMessageAsync(int rideId, SendChatMessageRequest request, CancellationToken cancellationToken = default);

        Task<PagedResult<ChatMessageDto>> GetMessagesAsync(int rideId, ChatMessagesQuery query, CancellationToken cancellationToken = default);

        Task<PagedResult<ChatConversationItemDto>> GetConversationsAsync(ChatConversationsQuery query, CancellationToken cancellationToken = default);
    }
}
