using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Trips;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripHistoryController : ControllerBase
    {
        private readonly IRideService _rideService;
        private readonly ILogger<TripHistoryController> _logger;

        public TripHistoryController(IRideService rideService, ILogger<TripHistoryController> logger)
        {
            _rideService = rideService;
            _logger = logger;
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<PagedResult<UserTripHistoryItem>>> GetUserHistory(
            int userId,
            [FromQuery] TripHistoryQuery query,
            CancellationToken ct)
        {
            try
            {
                var result = await _rideService.GetUserTripHistoryAsync(userId, query, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user trip history for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to load user trip history", details = ex.Message });
            }
        }

        [HttpGet("driver/{driverId}")]
        public async Task<ActionResult<PagedResult<DriverTripHistoryItem>>> GetDriverHistory(
            int driverId,
            [FromQuery] TripHistoryQuery query,
            CancellationToken ct)
        {
            try
            {
                var result = await _rideService.GetDriverTripHistoryAsync(driverId, query, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get driver trip history for driver {DriverId}", driverId);
                return StatusCode(500, new { error = "Failed to load driver trip history", details = ex.Message });
            }
        }
    }
}
