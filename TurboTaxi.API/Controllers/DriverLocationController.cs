using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Realtime;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/driver/location")]
    public class DriverLocationController : ControllerBase
    {
        private readonly IDriverLocationService _service;
        private readonly ILogger<DriverLocationController> _logger;

        public DriverLocationController(
            IDriverLocationService service,
            ILogger<DriverLocationController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] DriverLocationUpdateRequest request, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation($"[Location Update] Driver #{request.DriverId} - ({request.Latitude}, {request.Longitude})");
                
                if (request == null)
                {
                    _logger.LogWarning("[Location Update] Request is null");
                    return BadRequest(new { success = false, error = "Request body is required" });
                }

                if (request.DriverId <= 0)
                {
                    _logger.LogWarning($"[Location Update] Invalid DriverId: {request.DriverId}");
                    return BadRequest(new { success = false, error = "Valid DriverId is required" });
                }

                await _service.UpdateAsync(request, ct);
                
                _logger.LogInformation($"[Location Update] SUCCESS - Driver #{request.DriverId}");
                return Ok(new { success = true, message = "Location updated successfully" });
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"[Location Update] TIMEOUT - Driver #{request?.DriverId} - Redis connection timeout");
                
                return StatusCode(503, new
                {
                    success = false,
                    error = "Service temporarily unavailable",
                    message = "Redis connection timeout",
                    type = "TimeoutException"
                });
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, $"[Location Update] REDIS ERROR - Driver #{request?.DriverId} - {ex.Message}");
                
                return StatusCode(503, new
                {
                    success = false,
                    error = "Redis service error",
                    message = ex.Message,
                    type = "RedisException"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Location Update] FAILED - Driver #{request?.DriverId} - {ex.Message}");
                _logger.LogError($"[Location Update] Stack Trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"[Location Update] Inner Exception: {ex.InnerException.Message}");
                }
                
                return StatusCode(500, new
                {
                    success = false,
                    error = "Location update failed",
                    message = ex.Message,
                    type = ex.GetType().Name,
                    innerError = ex.InnerException?.Message
                });
            }
        }
    }
}
