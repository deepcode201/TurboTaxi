namespace TurboTaxi.Application.Interfaces
{
    public interface IRideNotificationService
    {
        Task NotifyDriverAsync(int driverId, object payload);
        Task NotifyUserAsync(int userId, object payload);
        Task NotifyNearestDriversAsync(List<int> driverIds, object payload);
        Task NotifyAllDriversRideCanceled(int rideId);
        Task NotifyOtherDriversRideTaken(int rideId, int acceptedDriverId);
    }
}
