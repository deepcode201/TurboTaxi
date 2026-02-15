using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Infrastructure.Services
{
    public class UserCrudService : IUserCrudService
    {
        private readonly ApplicationDbContext _db;
        public UserCrudService(ApplicationDbContext db) => _db = db;

        public async Task<UserListItemDto> UpdateAsync(int id, string fullName, string phoneNumber, string email, CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) throw new KeyNotFoundException("User not found");
            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.Email = email;
            user.UpdatedTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Map(user);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return false;
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<UserListItemDto?> GetByIdAsync(int id, CancellationToken ct)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
            return user is null ? null : Map(user);
        }

        public async Task<IReadOnlyList<UserListItemDto>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Users.AsNoTracking()
                .OrderByDescending(u => u.Id)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Email = u.Email,
                    CreatedAt = u.CreatedTime
                }).ToListAsync(ct);
        }

        private static UserListItemDto Map(User u) => new()
        {
            Id = u.Id,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            CreatedAt = u.CreatedTime
        };
    }
}
