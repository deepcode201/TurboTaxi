using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class DriverStatusHistory : BaseEntity
    {
        public int DriverId { get; set; }
        public DriverStatus OldStatus { get; set; }
        public DriverStatus NewStatus { get; set; }
        public DateTime ChangedTime { get; set; }

        // Navigation
        public Driver Driver { get; set; } = null!;
    }

}
