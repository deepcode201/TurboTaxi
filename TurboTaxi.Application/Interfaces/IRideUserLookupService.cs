namespace TurboTaxi.Application.Interfaces
{
    /// <summary>
    /// Helper service to lookup userId by rideId for real-time broadcasting
    /// </summary>
    public interface IRideUserLookupService
    {
        Task<int?> GetUserIdByRideIdAsync(int rideId, CancellationToken ct = default);
    }
}
