using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Realtime.Redis;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/debug/redis")]
    public class RedisDebugController : ControllerBase
    {
        private readonly IRedisService _redis;
        public RedisDebugController(IRedisService redis) => _redis = redis;

        [HttpGet("driver/{driverId:int}")]
        public async Task<IActionResult> GetDriver(int driverId)
        {
            var hashKey = RedisKeys.DriverHash(driverId);
            var heartbeatKey = RedisKeys.DriverHeartbeat(driverId);
            var hash = await _redis.HashGetAllAsync(hashKey);
            var dict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            dict.TryGetValue("vehicleType", out var vehicleType);
            vehicleType ??= "standard";
            var geoKey = RedisKeys.DriverGeo(vehicleType);
            var geoPos = await _redis.GeoPositionAsync(geoKey, driverId.ToString());
            var ttl = await _redis.GetTTLAsync(heartbeatKey);

            return Ok(new
            {
                driverId,
                hashKey,
                heartbeatKey,
                heartbeatTtlSeconds = ttl?.TotalSeconds,
                hash = dict,
                geoKey,
                geoPosition = geoPos is null ? null : new { geoPos.Value.Longitude, geoPos.Value.Latitude }
            });
        }

        [HttpGet("keys")]
        public async Task<IActionResult> Keys([FromQuery] string pattern = "driver:*")
        {
            var keys = await _redis.ListKeysAsync(pattern);
            return Ok(new { pattern, keys });
        }

        [HttpGet("geo/nearby")]
        public async Task<IActionResult> Nearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] string vehicleType = "standard", [FromQuery] double radiusKm = 5)
        {
            var geoKey = RedisKeys.DriverGeo(vehicleType);
            var results = await _redis.GeoRadiusAsync(geoKey, lng, lat, radiusKm);
            var data = results.Select(r => new
            {
                member = r.Member.ToString(),
                distanceKm = r.Distance,
                position = r.Position is null ? null : new { r.Position.Value.Longitude, r.Position.Value.Latitude }
            });
            return Ok(new { geoKey, count = results.Length, drivers = data });
        }
    }
}
