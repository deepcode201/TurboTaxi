using TurboTaxi.Models.Rides;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.Application.Interfaces
{
    public interface IRideService
    {
        Task<CreateRideResponse> CreateRideAsync(CreateRideRequest request, CancellationToken ct = default);
        Task<RideAcceptedResponse> AcceptRideAsync(int rideId, AcceptRideRequest request, CancellationToken ct = default);
        Task<StartRideResponse> StartRideAsync(int rideId, StartRideRequest request, CancellationToken ct = default);
        Task<RideFinishedResponse> FinishRideAsync(int rideId, FinishRideRequest request, CancellationToken ct = default);
        Task<CancelRideResponse> CancelRideByUserAsync(int rideId, CancelRideByUserRequest request, CancellationToken ct = default);
        Task<CancelRideResponse> CancelRideByDriverAsync(int rideId, CancelRideByDriverRequest request, CancellationToken ct = default);
        Task<PagedResult<UserTripHistoryItem>> GetUserTripHistoryAsync(int userId, TripHistoryQuery query, CancellationToken ct = default);
        Task<PagedResult<DriverTripHistoryItem>> GetDriverTripHistoryAsync(int driverId, TripHistoryQuery query, CancellationToken ct = default);
    }
}
