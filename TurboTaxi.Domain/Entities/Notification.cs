using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Notification : BaseEntity
    {
        public int? UserId { get; set; }
        public int? DriverId { get; set; }

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;

        public bool IsRead { get; set; } = false;
        public DateTime? ReadTime { get; set; }

        public NotificationType NotificationType { get; set; } = NotificationType.System;

        // Navigation
        public User? User { get; set; }
        public Driver? Driver { get; set; }
    }

}
