using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;

namespace TurboTaxi.Domain.Entities
{
    public class UserPromoCode : BaseEntity
    {
        public int UserId { get; set; }
        public int PromoCodeId { get; set; }

        public int UsedCount { get; set; } = 0;
        public DateTime? LastUsedTime { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public PromoCode PromoCode { get; set; } = null!;
    }

}
