using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.Interfaces
{
    public interface IUserCrudService
    {
        Task<UserListItemDto> UpdateAsync(int id, string fullName, string phoneNumber, string email, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<UserListItemDto?> GetByIdAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<UserListItemDto>> GetAllAsync(CancellationToken ct);
    }
}
