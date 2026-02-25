using TurboTaxi.Models.Rides;

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
    }
}
