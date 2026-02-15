using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;

namespace TurboTaxi.Domain.Entities
{
    public class Rating : BaseEntity
    {
        public int RideId { get; set; }

        public int FromUserId { get; set; }       // kim qiymət verir
        public int? ToDriverId { get; set; }      // user driver-i rate edir
        public int? ToUserId { get; set; }        // driver user-i rate edir (optional)

        public int Stars { get; set; }            // 1–5
        public string? Comment { get; set; }

        // Navigation
        public Ride Ride { get; set; } = null!;
        public User FromUser { get; set; } = null!;
        public Driver? ToDriver { get; set; }
        public User? ToUser { get; set; }
    }

}
