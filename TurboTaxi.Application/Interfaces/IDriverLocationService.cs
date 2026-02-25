using TurboTaxi.Models.Realtime;

namespace TurboTaxi.Application.Interfaces
{
    public interface IDriverLocationService
    {
        Task UpdateAsync(DriverLocationUpdateRequest request, CancellationToken ct = default);
    }
}
