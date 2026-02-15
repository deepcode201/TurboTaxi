using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
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

        [HttpPost("{rideId}/accept")]
        public async Task<ActionResult<RideAcceptedResponse>> AcceptRide(int rideId, [FromBody] AcceptRideRequest request, CancellationToken ct)
        {
            try
            {
                return Ok(await _rideService.AcceptRideAsync(rideId, request, ct));
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
                _logger.LogError(ex, $"Error accepting ride #{rideId}");
                return StatusCode(500, new { error = "Failed to accept ride", details = ex.Message });
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
