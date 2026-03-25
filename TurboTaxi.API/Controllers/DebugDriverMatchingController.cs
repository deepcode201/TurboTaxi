using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.API.Controllers
{
    /// <summary>
    /// Debug controller to diagnose driver matching issues.
    /// Only for development/debugging - remove in production.
    /// </summary>
    [ApiController]
    [Route("api/debug/driver-matching")]
    public class DebugDriverMatchingController : ControllerBase
    {
        private readonly IRedisService _redis;
        private readonly ILogger<DebugDriverMatchingController> _logger;

        public DebugDriverMatchingController(
            IRedisService redis,
            ILogger<DebugDriverMatchingController> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Get all drivers currently in Redis for a specific vehicle type.
        /// </summary>
        [HttpGet("drivers/{vehicleType}")]
        public async Task<IActionResult> GetDriversInGeo(int vehicleType)
        {
            try
            {
                var geoKey = $"geo:drivers:{((VehicleType)vehicleType).ToString().ToLowerInvariant()}";
                _logger.LogInformation($"[Debug] Querying geo index: {geoKey}");

                // Query a large radius to get all drivers
                var allDrivers = await _redis.GeoRadiusAsync(geoKey, 0, 0, 10000);
                _logger.LogInformation($"[Debug] Found {allDrivers.Length} drivers in geo index");

                var result = new List<object>();
                foreach (var d in allDrivers)
                {
                    var driverId = int.Parse(d.Member);
                    var hash = await _redis.HashGetAllAsync($"driver:{driverId}");
                    var data = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                    result.Add(new
                    {
                        driverId,
                        status = data.GetValueOrDefault("status"),
                        lat = data.GetValueOrDefault("lat"),
                        lng = data.GetValueOrDefault("lng"),
                        vehicleType = data.GetValueOrDefault("vehicleType"),
                        updatedAt = data.GetValueOrDefault("updatedAt")
                    });
                }

                return Ok(new
                {
                    geoKey,
                    totalDrivers = allDrivers.Length,
                    drivers = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Debug] Error querying drivers");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get matching drivers for a specific pickup location.
        /// </summary>
        [HttpPost("match")]
        public async Task<IActionResult> MatchDrivers([FromBody] DebugMatchRequest request)
        {
            try
            {
                _logger.LogInformation($"[Debug] Matching drivers for ({request.Lat}, {request.Lng}), radius={request.RadiusKm}km, vehicleType={request.VehicleType}");

                var geoKey = $"geo:drivers:{((VehicleType)request.VehicleType).ToString().ToLowerInvariant()}";
                var nearbyDrivers = await _redis.GeoRadiusAsync(geoKey, request.Lng, request.Lat, request.RadiusKm);

                _logger.LogInformation($"[Debug] GeoRadiusAsync returned {nearbyDrivers.Length} drivers");

                var cutoff = DateTime.UtcNow.AddSeconds(-60);
                var matched = new List<int>();
                var rejected = new List<object>();

                foreach (var r in nearbyDrivers)
                {
                    var did = int.Parse(r.Member);
                    var hash = await _redis.HashGetAllAsync($"driver:{did}");

                    if (hash.Length == 0)
                    {
                        rejected.Add(new { driverId = did, reason = "No data in Redis hash" });
                        continue;
                    }

                    var d = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                    var status = d.GetValueOrDefault("status");
                    var updatedAtStr = d.GetValueOrDefault("updatedAt");

                    if (status != "Free")
                    {
                        rejected.Add(new { driverId = did, reason = $"Status is '{status}', not 'Free'" });
                        continue;
                    }

                    if (!DateTime.TryParse(updatedAtStr, out var updatedAt))
                    {
                        rejected.Add(new { driverId = did, reason = $"Invalid updatedAt format: '{updatedAtStr}'" });
                        continue;
                    }

                    if (updatedAt <= cutoff)
                    {
                        rejected.Add(new { driverId = did, reason = $"Stale (updatedAt={updatedAt:O} <= {cutoff:O})" });
                        continue;
                    }

                    matched.Add(did);
                }

                return Ok(new
                {
                    location = new { lat = request.Lat, lng = request.Lng },
                    radiusKm = request.RadiusKm,
                    vehicleType = request.VehicleType,
                    geoRadiusFound = nearbyDrivers.Length,
                    cutoffTime = cutoff.ToString("O"),
                    matched = new { count = matched.Count, driverIds = matched },
                    rejected = new { count = rejected.Count, details = rejected }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Debug] Error matching drivers");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed info about a specific driver.
        /// </summary>
        [HttpGet("driver/{driverId}")]
        public async Task<IActionResult> GetDriverInfo(int driverId)
        {
            try
            {
                _logger.LogInformation($"[Debug] Getting info for driver #{driverId}");

                var hash = await _redis.HashGetAllAsync($"driver:{driverId}");
                if (hash.Length == 0)
                {
                    return NotFound(new { error = $"Driver #{driverId} not found in Redis" });
                }

                var data = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                // Parse lat/lng to check for 0 values
                var lat = data.GetValueOrDefault("lat");
                var lng = data.GetValueOrDefault("lng");
                var isZeroCoordinates = lat == "0" && lng == "0";

                // Also check geo indices
                var geoData = new Dictionary<string, object>();
                foreach (VehicleType vt in Enum.GetValues(typeof(VehicleType)))
                {
                    var geoKey = $"geo:drivers:{vt.ToString().ToLowerInvariant()}";
                    var pos = await _redis.GeoPositionAsync(geoKey, driverId.ToString());
                    if (pos.HasValue)
                    {
                        geoData[vt.ToString()] = new { lat = pos.Value.Latitude, lng = pos.Value.Longitude };
                    }
                }

                return Ok(new
                {
                    driverId,
                    warning = isZeroCoordinates ? "⚠️ CRITICAL: Driver coordinates are (0, 0) - Location mapping is BROKEN!" : null,
                    redisHash = data,
                    geoIndices = geoData,
                    diagnostics = new
                    {
                        hasLocation = !isZeroCoordinates,
                        coordinatesAreZero = isZeroCoordinates,
                        inGeoIndex = geoData.Count > 0,
                        statusValue = data.GetValueOrDefault("status"),
                        updatedAt = data.GetValueOrDefault("updatedAt")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Debug] Error getting driver info");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class DebugMatchRequest
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double RadiusKm { get; set; } = 3.0;
        public int VehicleType { get; set; } = 0; // Eco
    }
}
