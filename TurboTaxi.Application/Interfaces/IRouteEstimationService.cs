using TurboTaxi.Models.Routes;

namespace TurboTaxi.Application.Interfaces
{
    public interface IRouteEstimationService
    {
        Task<RouteEstimateResponse> EstimateAsync(RouteEstimateRequest request, CancellationToken ct = default);
    }
}
