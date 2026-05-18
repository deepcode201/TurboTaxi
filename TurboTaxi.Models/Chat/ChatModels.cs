namespace TurboTaxi.Models.Chat
{
    public class SendChatMessageRequest
    {
        /// <summary>"user" (passenger app) or "driver".</summary>
        public string ActorRole { get; set; } = null!;

        /// <summary>Passenger: User.Id. Driver app: Driver.Id.</summary>
        public int ActorId { get; set; }

        public string Text { get; set; } = null!;
    }

    public class ChatMessagesQuery
    {
        public string ActorRole { get; set; } = null!;
        public int ActorId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ChatConversationsQuery
    {
        public string ActorRole { get; set; } = null!;
        public int ActorId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int RideId { get; set; }
        public string SenderRole { get; set; } = null!;
        public int SenderId { get; set; }
        public string Text { get; set; } = null!;
        public DateTime SentAtUtc { get; set; }
    }

    /// <summary>One row per ride where at least one message was exchanged.</summary>
    public class ChatConversationItemDto
    {
        public int RideId { get; set; }

        /// <summary>"driver" when viewer is passenger; "user" when viewer is driver.</summary>
        public string CounterpartyRole { get; set; } = null!;

        /// <summary>Driver.Id or User.Id (passenger) matching CounterpartyRole.</summary>
        public int CounterpartyId { get; set; }

        public string CounterpartyDisplayName { get; set; } = null!;
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAtUtc { get; set; }
        public string? LastMessageSenderRole { get; set; }
    }
}
