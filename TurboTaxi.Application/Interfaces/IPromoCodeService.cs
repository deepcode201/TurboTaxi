using TurboTaxi.Models.PromoCodes;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.Application.Interfaces
{
    public interface IPromoCodeService
    {
        Task<CreatePromoCodeResponse> CreateAsync(CreatePromoCodeRequest request, CancellationToken ct = default);
        Task<ApplyPromoCodeResponse> ApplyAsync(ApplyPromoCodeRequest request, CancellationToken ct = default);
        Task DeleteAsync(int promoCodeId, CancellationToken ct = default);
        Task<PagedResult<UserPromoCodeDto>> GetUserPromoCodesAsync(PromoCodeListQuery query, CancellationToken ct = default);

        /// <summary>
        /// Finds the user's active promo and calculates discounted fare.
        /// When <paramref name="consumeUsage"/> is true, increments UsedCount and deactivates promo when limit is reached.
        /// </summary>
        Task<PromoFareResult> ApplyActivePromoAsync(
            int userId,
            decimal originalFare,
            bool consumeUsage,
            CancellationToken ct = default);
    }
}
