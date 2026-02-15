using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Infrastructure.Services
{
    public class DriverCrudService : IDriverCrudService
    {
        private readonly ApplicationDbContext _db;
        public DriverCrudService(ApplicationDbContext db) => _db = db;

        public async Task<DriverDto> CreateAsync(DriverCreateRequest request, CancellationToken ct)
        {
            var entity = new Driver
            {
                UserId = request.UserId,
                IsVerified = request.IsVerified,
                VehicleId = request.VehicleId
            };
            _db.Drivers.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Map(entity);
        }

        public async Task<DriverDto> UpdateAsync(int id, DriverUpdateRequest request, CancellationToken ct)
        {
            var entity = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (entity is null) throw new KeyNotFoundException("Driver not found");
            if (request.IsVerified.HasValue) entity.IsVerified = request.IsVerified.Value;
            entity.VehicleId = request.VehicleId;
            entity.UpdatedTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Map(entity);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (entity is null) return false;
            _db.Drivers.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<DriverDto?> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            return entity is null ? null : Map(entity);
        }

        public async Task<IReadOnlyList<DriverDto>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Drivers.AsNoTracking()
                .OrderByDescending(d => d.Id)
                .Select(d => new DriverDto
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    IsVerified = d.IsVerified,
                    VehicleId = d.VehicleId,
                    CreatedAt = d.CreatedTime,
                    UpdatedAt = d.UpdatedTime
                })
                .ToListAsync(ct);
        }

        private static DriverDto Map(Driver d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            IsVerified = d.IsVerified,
            VehicleId = d.VehicleId,
            CreatedAt = d.CreatedTime,
            UpdatedAt = d.UpdatedTime
        };
    }
}
