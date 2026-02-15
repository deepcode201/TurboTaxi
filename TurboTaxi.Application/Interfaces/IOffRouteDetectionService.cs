using System.Threading;
using System.Threading.Tasks;

namespace TurboTaxi.Application.Interfaces
{
    public interface IOffRouteDetectionService
    {
        /// <summary>
        /// Check if driver is off-route and recalculate if needed.
        /// Returns true if route was recalculated.
        /// </summary>
        Task<bool> CheckAndRecalculateIfOffRouteAsync(
            int driverId, 
            double currentLat, 
            double currentLng, 
            CancellationToken ct = default);
    }
}
