using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Infrastructure.Services
{
    public class VehicleCrudService : IVehicleCrudService
    {
        private readonly ApplicationDbContext _db;
        public VehicleCrudService(ApplicationDbContext db) => _db = db;

        public async Task<VehicleDto> CreateAsync(VehicleCreateRequest request, CancellationToken ct)
        {
            var vehicle = new Vehicle
            {
                PlateNumber = request.PlateNumber,
                Brand = request.Brand,
                Model = request.Model,
                Color = request.Color,
                Year = request.Year,
                VehicleType = Enum.TryParse<VehicleType>(request.VehicleType, true, out var vt) ? vt : VehicleType.Standard
            };
            _db.Vehicles.Add(vehicle);
            await _db.SaveChangesAsync(ct);
            return Map(vehicle);
        }

        public async Task<VehicleDto> UpdateAsync(int id, VehicleUpdateRequest request, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (vehicle is null) throw new KeyNotFoundException("Vehicle not found");
            vehicle.PlateNumber = request.PlateNumber;
            vehicle.Brand = request.Brand;
            vehicle.Model = request.Model;
            vehicle.Color = request.Color;
            vehicle.Year = request.Year;
            vehicle.VehicleType = Enum.TryParse<VehicleType>(request.VehicleType, true, out var vt) ? vt : vehicle.VehicleType;
            vehicle.UpdatedTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Map(vehicle);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (vehicle is null) return false;
            _db.Vehicles.Remove(vehicle);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<VehicleDto?> GetByIdAsync(int id, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
            return vehicle is null ? null : Map(vehicle);
        }

        public async Task<IReadOnlyList<VehicleDto>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Vehicles.AsNoTracking()
                .OrderByDescending(v => v.Id)
                .Select(v => new VehicleDto
                {
                    Id = v.Id,
                    PlateNumber = v.PlateNumber,
                    Brand = v.Brand,
                    Model = v.Model,
                    Color = v.Color,
                    Year = v.Year,
                    VehicleType = v.VehicleType.ToString(),
                    CreatedAt = v.CreatedTime
                }).ToListAsync(ct);
        }

        private static VehicleDto Map(Vehicle v) => new()
        {
            Id = v.Id,
            PlateNumber = v.PlateNumber,
            Brand = v.Brand,
            Model = v.Model,
            Color = v.Color,
            Year = v.Year,
            VehicleType = v.VehicleType.ToString(),
            CreatedAt = v.CreatedTime
        };
    }
}
