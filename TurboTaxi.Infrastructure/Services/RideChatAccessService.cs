using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Infrastructure.Data;

namespace TurboTaxi.Infrastructure.Services
{
    public sealed class RideChatAccessService : IRideChatAccess
    {
        private readonly ApplicationDbContext _db;

        public RideChatAccessService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CanAccessAsync(int rideId, string actorRole, int actorId, CancellationToken cancellationToken = default)
        {
            var ride = await _db.Rides
                .AsNoTracking()
                .Select(r => new { r.Id, r.UserId, r.DriverId })
                .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

            if (ride == null || !ride.DriverId.HasValue)
                return false;

            if (string.Equals(actorRole, "user", StringComparison.OrdinalIgnoreCase))
                return ride.UserId == actorId;

            if (string.Equals(actorRole, "driver", StringComparison.OrdinalIgnoreCase))
                return ride.DriverId == actorId;

            return false;
        }
    }
}
