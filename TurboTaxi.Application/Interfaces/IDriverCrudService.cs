using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.Interfaces
{
    public interface IDriverCrudService
    {
        Task<DriverDto> CreateAsync(DriverCreateRequest request, CancellationToken ct);
        Task<DriverDto> UpdateAsync(int id, DriverUpdateRequest request, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<DriverDto?> GetByIdAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<DriverDto>> GetAllAsync(CancellationToken ct);
    }
}
