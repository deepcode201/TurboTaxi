using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Models.PromoCodes
{
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
