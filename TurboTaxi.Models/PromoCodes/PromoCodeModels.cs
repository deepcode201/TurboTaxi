using TurboTaxi.Domain.Enums;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.Models.PromoCodes
{
    public class PromoCodeListQuery
    {
        public int? UserId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        /// <summary>When true, only promos that are active and not exhausted for this user.</summary>
        public bool? ActiveOnly { get; set; }
    }

    public class UserPromoCodeDto
    {
        public int UserPromoCodeId { get; set; }
        public int UserId { get; set; }
        public int PromoCodeId { get; set; }
        public string Code { get; set; } = null!;
        public string? Description { get; set; }
        public PromoDiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxUsagePerUser { get; set; }
        public int? MaxUsageCount { get; set; }
        public int UsedCount { get; set; }
        public int? RemainingRideCount { get; set; }
        public bool IsActive { get; set; }
        public bool CanUse { get; set; }
        public DateTime? LastUsedTime { get; set; }
        public DateTime CreatedTime { get; set; }
    }
    public class CreatePromoCodeRequest
    {
        public bool IsPercentage { get; set; }
        public bool IsAmount { get; set; }
        public decimal Number { get; set; }
        public int UserId { get; set; }
        public int ExpireRideCount { get; set; }
    }

    public class CreatePromoCodeResponse
    {
        public int PromoCodeId { get; set; }
        public string Code { get; set; } = null!;
        public PromoDiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public int UserId { get; set; }
        public int ExpireRideCount { get; set; }
    }

    public class ApplyPromoCodeRequest
    {
        public int UserId { get; set; }
        public int PromoCodeId { get; set; }
        public decimal OriginalFare { get; set; }
    }

    public class ApplyPromoCodeResponse
    {
        public int UserId { get; set; }
        public int PromoCodeId { get; set; }
        public decimal OriginalFare { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalFare { get; set; }
    }

    /// <summary>Result of applying (or previewing) the user's active promo on a fare.</summary>
    public class PromoFareResult
    {
        public bool Applied { get; set; }
        public int? PromoCodeId { get; set; }
        public string? PromoCode { get; set; }
        public decimal OriginalFare { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalFare { get; set; }
        /// <summary>Remaining rides for this promo after apply (null if not applied).</summary>
        public int? RemainingRideCount { get; set; }
    }
}
