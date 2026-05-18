using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Chat;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>Send a chat message for a ride (persisted + broadcast over SignalR).</summary>
        [HttpPost("rides/{rideId:int}/messages")]
        public async Task<ActionResult<ChatMessageDto>> SendMessage(int rideId, [FromBody] SendChatMessageRequest request, CancellationToken ct)
        {
            try
            {
                var dto = await _chatService.SendMessageAsync(rideId, request, ct);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST chat message failed for ride #{RideId}", rideId);
                return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
            }
        }

        /// <summary>Load message history (newest page first). Query: actorRole, actorId, pageNumber, pageSize.</summary>
        [HttpGet("rides/{rideId:int}/messages")]
        public async Task<ActionResult<PagedResult<ChatMessageDto>>> GetMessages(
            int rideId,
            [FromQuery] string actorRole,
            [FromQuery] int actorId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            try
            {
                var query = new ChatMessagesQuery
                {
                    ActorRole = actorRole,
                    ActorId = actorId,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
                return Ok(await _chatService.GetMessagesAsync(rideId, query, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET chat messages failed for ride #{RideId}", rideId);
                return StatusCode(500, new { error = "Failed to load messages", details = ex.Message });
            }
        }

        /// <summary>Rides where this user or driver has exchanged at least one message (paginated).</summary>
        [HttpGet("conversations")]
        public async Task<ActionResult<PagedResult<ChatConversationItemDto>>> GetConversations(
            [FromQuery] string actorRole,
            [FromQuery] int actorId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                var query = new ChatConversationsQuery
                {
                    ActorRole = actorRole,
                    ActorId = actorId,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
                return Ok(await _chatService.GetConversationsAsync(query, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET chat conversations failed");
                return StatusCode(500, new { error = "Failed to load conversations", details = ex.Message });
            }
        }
    }
}
