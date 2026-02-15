namespace TurboTaxi.Application.Interfaces
{
    /// <summary>
    /// Service for checking pending rides when driver location updates
    /// </summary>
    public interface IPendingRideChecker
    {
        Task CheckNearbyPendingRidesAsync(int driverId, double latitude, double longitude, string vehicleType, CancellationToken ct = default);
    }
}
