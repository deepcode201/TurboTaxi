using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.Interfaces
{
    public interface IVehicleCrudService
    {
        Task<VehicleDto> CreateAsync(VehicleCreateRequest request, CancellationToken ct);
        Task<VehicleDto> UpdateAsync(int id, VehicleUpdateRequest request, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<VehicleDto?> GetByIdAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<VehicleDto>> GetAllAsync(CancellationToken ct);
    }
}
