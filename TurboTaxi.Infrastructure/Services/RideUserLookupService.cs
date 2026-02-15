using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Infrastructure.Data;

namespace TurboTaxi.Infrastructure.Services
{
    /// <summary>
    /// Helper service to lookup userId by rideId for real-time broadcasting
    /// </summary>
    public class RideUserLookupService : IRideUserLookupService
    {
        private readonly ApplicationDbContext _db;

        public RideUserLookupService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<int?> GetUserIdByRideIdAsync(int rideId, CancellationToken ct = default)
        {
            var ride = await _db.Rides
                .Where(r => r.Id == rideId)
                .Select(r => r.UserId)
                .FirstOrDefaultAsync(ct);

            return ride == 0 ? null : ride;
        }
    }
}
