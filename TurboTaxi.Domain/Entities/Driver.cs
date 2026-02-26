using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Driver : BaseEntity
    {
        public int UserId { get; set; }             // FK → User
        public bool IsVerified { get; set; } = false; // sənəd təsdiqi və s.

        // Aktual maşın
        public int? VehicleId { get; set; }

        // Real-time tərəfi Redis-də olacaq, amma DB üçün son known location (optional)


        // Navigation
        public User User { get; set; } = null!;
        public Vehicle? Vehicle { get; set; }
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
        public ICollection<DriverStatusHistory> StatusHistory { get; set; } = new List<DriverStatusHistory>();
        public ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>(); // user-rated
    }

}
