using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Helpers;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Rides;

namespace TurboTaxi.API.Controllers
{
    /// <summary>
    /// Legacy/alternate URLs used by some mobile builds (e.g. /api/drivers/accept/123).
    /// </summary>
    [ApiController]
    [Route("api/drivers")]
    public class DriverRidesController : ControllerBase
    {
        private readonly IRideService _rideService;
        private readonly ILogger<DriverRidesController> _logger;

        public DriverRidesController(IRideService rideService, ILogger<DriverRidesController> logger)
        {
            _rideService = rideService;
            _logger = logger;
        }

        [HttpPost("accept/{rideIdOrRequestId}")]
        [HttpPost("rides/{rideIdOrRequestId}/accept")]
        public Task<ActionResult<RideAcceptedResponse>> AcceptLegacy(
            string rideIdOrRequestId,
            [FromBody] AcceptRideRequest? request,
            CancellationToken ct)
            => AcceptInternal(rideIdOrRequestId, request, ct);

        private async Task<ActionResult<RideAcceptedResponse>> AcceptInternal(
            string rideIdOrRequestId,
            AcceptRideRequest? request,
            CancellationToken ct)
        {
            try
            {
                var rideId = RideIdParser.Parse(rideIdOrRequestId);
                request ??= new AcceptRideRequest();

                if (request.DriverId <= 0)
                    return BadRequest(new { success = false, error = "driverId is required in request body" });

                _logger.LogInformation(
                    "Legacy accept: rideId={RideId}, driverId={DriverId}",
                    rideId, request.DriverId);

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
                _logger.LogError(ex, "Legacy accept failed for {RideIdOrRequestId}", rideIdOrRequestId);
                return StatusCode(500, new { success = false, error = "Failed to accept ride", details = ex.Message });
            }
        }
    }
}
