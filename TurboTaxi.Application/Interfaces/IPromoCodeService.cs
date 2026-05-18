using TurboTaxi.Models.PromoCodes;

namespace TurboTaxi.Application.Interfaces
{
    public interface IPromoCodeService
    {
        Task<CreatePromoCodeResponse> CreateAsync(CreatePromoCodeRequest request, CancellationToken ct = default);
        Task<ApplyPromoCodeResponse> ApplyAsync(ApplyPromoCodeRequest request, CancellationToken ct = default);
        Task DeleteAsync(int promoCodeId, CancellationToken ct = default);
    }
}
