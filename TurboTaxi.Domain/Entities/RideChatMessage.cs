using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class RideChatMessage : BaseEntity
    {
        public int RideId { get; set; }
        public ChatSenderRole SenderRole { get; set; }
        /// <summary>User.Id if SenderRole is User; Driver.Id if SenderRole is Driver.</summary>
        public int SenderId { get; set; }

        public string Body { get; set; } = null!;

        public Ride Ride { get; set; } = null!;
    }
}
