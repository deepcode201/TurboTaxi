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
}
