using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Chat;
using TurboTaxi.Models.Trips;
using TurboTaxi.Realtime.Hubs;

namespace TurboTaxi.Infrastructure.Services
{
    public sealed class ChatService : IChatService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRideChatAccess _access;
        private readonly IHubContext<ChatHub, IChatClient> _chatHub;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ApplicationDbContext db,
            IRideChatAccess access,
            IHubContext<ChatHub, IChatClient> chatHub,
            ILogger<ChatService> logger)
        {
            _db = db;
            _access = access;
            _chatHub = chatHub;
            _logger = logger;
        }

        private static int NormalizePageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;

        private static int NormalizePageSize(int pageSize, int max)
        {
            if (pageSize < 1)
                return 20;
            return pageSize > max ? max : pageSize;
        }

        public async Task<ChatMessageDto> SendMessageAsync(int rideId, SendChatMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new InvalidOperationException("Message text is required.");

            var text = request.Text.Trim();
            if (text.Length > 4000)
                throw new InvalidOperationException("Message text is too long (max 4000 characters).");

            if (!await _access.CanAccessAsync(rideId, request.ActorRole, request.ActorId, cancellationToken))
                throw new InvalidOperationException("You are not allowed to chat on this ride or the ride has no driver yet.");

            var (role, senderId) = ParseActor(request.ActorRole, request.ActorId);
            var entity = new RideChatMessage
            {
                RideId = rideId,
                SenderRole = role,
                SenderId = senderId,
                Body = text,
                CreatedTime = DateTime.UtcNow
            };

            _db.RideChatMessages.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = MapMessage(entity);
            try
            {
                await _chatHub.Clients.Group(ChatHub.GroupForRide(rideId)).ReceiveChatMessage(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chat saved but SignalR broadcast failed for ride #{RideId}", rideId);
            }

            return dto;
        }

        public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync(int rideId, ChatMessagesQuery query, CancellationToken cancellationToken = default)
        {
            if (!await _access.CanAccessAsync(rideId, query.ActorRole, query.ActorId, cancellationToken))
                throw new InvalidOperationException("You are not allowed to read this chat.");

            var page = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize, 100);
            var skip = (page - 1) * pageSize;

            var baseQuery = _db.RideChatMessages.AsNoTracking().Where(m => m.RideId == rideId);
            var total = await baseQuery.CountAsync(cancellationToken);

            var rows = await baseQuery
                .OrderByDescending(m => m.CreatedTime)
                .ThenByDescending(m => m.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = rows.Select(MapMessage).ToList();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0)
                totalPages = 1;

            return new PagedResult<ChatMessageDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = totalPages
            };
        }

        public async Task<PagedResult<ChatConversationItemDto>> GetConversationsAsync(ChatConversationsQuery query, CancellationToken cancellationToken = default)
        {
            var page = NormalizePageNumber(query.PageNumber);
            var pageSize = NormalizePageSize(query.PageSize, 50);
            var skip = (page - 1) * pageSize;

            var isUser = string.Equals(query.ActorRole, "user", StringComparison.OrdinalIgnoreCase);
            var isDriver = string.Equals(query.ActorRole, "driver", StringComparison.OrdinalIgnoreCase);
            if (!isUser && !isDriver)
                throw new InvalidOperationException("ActorRole must be 'user' or 'driver'.");

            IQueryable<int> rideIdsQuery;
            if (isUser)
            {
                rideIdsQuery =
                    from m in _db.RideChatMessages.AsNoTracking()
                    join r in _db.Rides.AsNoTracking() on m.RideId equals r.Id
                    where r.UserId == query.ActorId && r.DriverId != null
                    select m.RideId;
            }
            else
            {
                rideIdsQuery =
                    from m in _db.RideChatMessages.AsNoTracking()
                    join r in _db.Rides.AsNoTracking() on m.RideId equals r.Id
                    where r.DriverId == query.ActorId
                    select m.RideId;
            }

            var distinctRideIds = rideIdsQuery.Distinct();

            var total = await distinctRideIds.CountAsync(cancellationToken);

            var orderedRideSummaries =
                from m in _db.RideChatMessages.AsNoTracking()
                join r in _db.Rides.AsNoTracking() on m.RideId equals r.Id
                where (isUser && r.UserId == query.ActorId && r.DriverId != null)
                      || (!isUser && r.DriverId == query.ActorId)
                group m by m.RideId
                into g
                select new { RideId = g.Key, LastAt = g.Max(x => x.CreatedTime) };

            var pageRows = await orderedRideSummaries
                .OrderByDescending(x => x.LastAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var rideIds = pageRows.Select(x => x.RideId).ToList();
            if (rideIds.Count == 0)
            {
                return new PagedResult<ChatConversationItemDto>
                {
                    Items = Array.Empty<ChatConversationItemDto>(),
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))
                };
            }

            var rides = await _db.Rides.AsNoTracking()
                .Where(r => rideIds.Contains(r.Id))
                .Include(r => r.User)
                .Include(r => r.Driver)!.ThenInclude(d => d!.User)
                .ToListAsync(cancellationToken);

            var ridesById = rides.ToDictionary(r => r.Id);

            var lastMessageLookup = new Dictionary<int, RideChatMessage>();
            foreach (var rid in rideIds)
            {
                var last = await _db.RideChatMessages.AsNoTracking()
                    .Where(m => m.RideId == rid)
                    .OrderByDescending(m => m.CreatedTime)
                    .ThenByDescending(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (last != null)
                    lastMessageLookup[rid] = last;
            }

            var items = new List<ChatConversationItemDto>(pageRows.Count);
            foreach (var row in pageRows.OrderByDescending(x => x.LastAt))
            {
                if (!ridesById.TryGetValue(row.RideId, out var ride))
                    continue;

                lastMessageLookup.TryGetValue(row.RideId, out var lastMsg);

                if (isUser)
                {
                    if (!ride.DriverId.HasValue || ride.Driver?.User == null)
                        continue;

                    var du = ride.Driver.User;
                    items.Add(new ChatConversationItemDto
                    {
                        RideId = ride.Id,
                        CounterpartyRole = "driver",
                        CounterpartyId = ride.DriverId.Value,
                        CounterpartyDisplayName = $"{du.Name} {du.Surname}".Trim(),
                        LastMessagePreview = lastMsg == null ? null : TruncatePreview(lastMsg.Body),
                        LastMessageAtUtc = lastMsg?.CreatedTime,
                        LastMessageSenderRole = lastMsg == null ? null : lastMsg.SenderRole == ChatSenderRole.User ? "user" : "driver"
                    });
                }
                else
                {
                    var u = ride.User;
                    items.Add(new ChatConversationItemDto
                    {
                        RideId = ride.Id,
                        CounterpartyRole = "user",
                        CounterpartyId = ride.UserId,
                        CounterpartyDisplayName = $"{u.Name} {u.Surname}".Trim(),
                        LastMessagePreview = lastMsg == null ? null : TruncatePreview(lastMsg.Body),
                        LastMessageAtUtc = lastMsg?.CreatedTime,
                        LastMessageSenderRole = lastMsg == null ? null : lastMsg.SenderRole == ChatSenderRole.User ? "user" : "driver"
                    });
                }
            }

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0)
                totalPages = 1;

            return new PagedResult<ChatConversationItemDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = totalPages
            };
        }

        private static string? TruncatePreview(string body)
        {
            if (string.IsNullOrEmpty(body))
                return body;
            const int max = 160;
            return body.Length <= max ? body : body[..max] + "…";
        }

        private static (ChatSenderRole role, int senderId) ParseActor(string actorRole, int actorId)
        {
            if (string.Equals(actorRole, "user", StringComparison.OrdinalIgnoreCase))
                return (ChatSenderRole.User, actorId);

            if (string.Equals(actorRole, "driver", StringComparison.OrdinalIgnoreCase))
                return (ChatSenderRole.Driver, actorId);

            throw new InvalidOperationException("ActorRole must be 'user' or 'driver'.");
        }

        private static ChatMessageDto MapMessage(RideChatMessage m) => new()
        {
            Id = m.Id,
            RideId = m.RideId,
            SenderRole = m.SenderRole == ChatSenderRole.User ? "user" : "driver",
            SenderId = m.SenderId,
            Text = m.Body,
            SentAtUtc = m.CreatedTime
        };
    }
}
