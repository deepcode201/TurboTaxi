using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Helpers;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Models.Rides;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RidesController : ControllerBase
    {
        private readonly IRideService _rideService;
        private readonly ILogger<RidesController> _logger;
        
        public RidesController(IRideService rideService, ILogger<RidesController> logger)
        {
            _rideService = rideService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreateRideResponse>> CreateRide([FromBody] CreateRideRequest request, CancellationToken ct)
        {
            if (!Enum.IsDefined(typeof(VehicleType), request.VehicleType))
            {
                var valid = string.Join(", ", Enum.GetValues<VehicleType>().Select(v => $"{v}"));
                return BadRequest(new { error = $"Invalid VehicleType: '{request.VehicleType}'. Valid: {valid}" });
            }

            try
            {
                _logger.LogInformation($"?? POST /api/rides - Creating ride for User #{request.UserId}");
                var result = await _rideService.CreateRideAsync(request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? Error creating ride");
                return StatusCode(500, new { error = "Failed to create ride", details = ex.Message });
            }
        }

        /// <summary>Accept by URL ride id (123) or SignalR requestId (ride_123).</summary>
        [HttpPost("{rideIdOrRequestId}/accept")]
        public Task<ActionResult<RideAcceptedResponse>> AcceptRide(
            string rideIdOrRequestId,
            [FromBody] AcceptRideRequest? request,
            CancellationToken ct)
            => AcceptRideCore(rideIdOrRequestId, request, ct);

        /// <summary>Accept when mobile app sends rideId only in JSON body.</summary>
        [HttpPost("accept")]
        public async Task<ActionResult<RideAcceptedResponse>> AcceptRideFromBody(
            [FromBody] AcceptRideRequest request,
            CancellationToken ct)
        {
            if (request.RideId <= 0 && string.IsNullOrWhiteSpace(request.RequestId))
                return BadRequest(new { success = false, error = "rideId or requestId is required in body" });

            var rideKey = request.RideId > 0
                ? request.RideId.ToString()
                : request.RequestId!;

            return await AcceptRideCore(rideKey, request, ct);
        }

        private async Task<ActionResult<RideAcceptedResponse>> AcceptRideCore(
            string rideIdOrRequestId,
            AcceptRideRequest? request,
            CancellationToken ct)
        {
            request ??= new AcceptRideRequest();

            try
            {
                if (request.DriverId <= 0)
                    return BadRequest(new { success = false, error = "driverId is required (body: driverId)" });

                var rideId = request.RideId > 0
                    ? request.RideId
                    : RideIdParser.Parse(rideIdOrRequestId);

                _logger.LogInformation(
                    "POST accept rideId={RideId} driverId={DriverId} lat={Lat} lng={Lng}",
                    rideId, request.DriverId, request.DriverLat, request.DriverLng);

                var result = await _rideService.AcceptRideAsync(rideId, request, ct);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting ride {RideIdOrRequestId}", rideIdOrRequestId);
                return StatusCode(500, new { success = false, error = "Failed to accept ride", details = ex.Message });
            }
        }

        [HttpPost("{rideId}/start")]
        public async Task<ActionResult<StartRideResponse>> StartRide(int rideId, [FromBody] StartRideRequest request, CancellationToken ct)
        {
            try
            {
                return Ok(await _rideService.StartRideAsync(rideId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting ride #{rideId}");
                return StatusCode(500, new { error = "Failed to start ride", details = ex.Message });
            }
        }

        [HttpPost("{rideId}/finish")]
        public async Task<ActionResult<RideFinishedResponse>> FinishRide(int rideId, [FromBody] FinishRideRequest request, CancellationToken ct)
        {
            try
            {
                return Ok(await _rideService.FinishRideAsync(rideId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finishing ride #{rideId}");
                return StatusCode(500, new { error = "Failed to finish ride", details = ex.Message });
            }
        }

        [HttpPost("{rideId}/cancel-by-user")]
        public async Task<ActionResult<CancelRideResponse>> CancelByUser(int rideId, [FromBody] CancelRideByUserRequest request, CancellationToken ct)
        {
            try
            {
                return Ok(await _rideService.CancelRideByUserAsync(rideId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error canceling ride #{rideId} by user");
                return StatusCode(500, new { error = "Failed to cancel ride", details = ex.Message });
            }
        }

        [HttpPost("{rideId}/cancel-by-driver")]
        public async Task<ActionResult<CancelRideResponse>> CancelByDriver(int rideId, [FromBody] CancelRideByDriverRequest request, CancellationToken ct)
        {
            try
            {
                return Ok(await _rideService.CancelRideByDriverAsync(rideId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error canceling ride #{rideId} by driver");
                return StatusCode(500, new { error = "Failed to cancel ride", details = ex.Message });
            }
        }
    }
}
