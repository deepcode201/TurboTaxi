using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class RideStatusHistory : BaseEntity
    {
        public int RideId { get; set; }
        public RideStatus OldStatus { get; set; }
        public RideStatus NewStatus { get; set; }
        public DateTime ChangedTime { get; set; }

        public string? ChangedByType { get; set; } // "User", "Driver", "System"
        public int? ChangedByUserId { get; set; }

        // Navigation
        public Ride Ride { get; set; } = null!;
    }

}
