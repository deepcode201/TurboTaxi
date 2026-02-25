using System;
using System.Collections.Generic;
using System.Text;
using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class PromoCode : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string? Description { get; set; }

        public PromoDiscountType DiscountType { get; set; } = PromoDiscountType.FixedAmount;
        public decimal DiscountValue { get; set; }          // 10 AZN və ya 10%
        public decimal? MaxDiscountAmount { get; set; }     // faiz olanda limit

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? MaxUsageCount { get; set; }             // ümumi sistem üzrə
        public int? MaxUsagePerUser { get; set; }           // user üçün limit

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<UserPromoCode> UserPromoCodes { get; set; } = new List<UserPromoCode>();
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
    }

}
