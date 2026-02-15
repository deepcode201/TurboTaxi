using StackExchange.Redis;
using System.Linq;
using System.Collections.Generic;

namespace TurboTaxi.Realtime.Redis
{
    public interface IRedisService
    {
        Task HashSetAsync(string key, HashEntry[] entries);
        Task<HashEntry[]> HashGetAllAsync(string key);
        Task<bool> SetExpiryAsync(string key, TimeSpan ttl);
        Task<bool> SetStringIfNotExistsAsync(string key, string value, TimeSpan? expiry = null);
        Task GeoAddAsync(string key, double longitude, double latitude, string member);
        Task<GeoRadiusResult[]> GeoRadiusAsync(string key, double longitude, double latitude, double radiusKm);
        Task<string?> HashGetAsync(string key, string field);
        Task HashDeleteAsync(string key, string field);
        // Debug helpers
        Task<TimeSpan?> GetTTLAsync(string key);
        Task<GeoPosition?> GeoPositionAsync(string key, string member);
        Task<IEnumerable<string>> ListKeysAsync(string pattern);
    }

    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer _mux;
        public RedisService(IConnectionMultiplexer mux) => _mux = mux;
        private IDatabase Db => _mux.GetDatabase();

        public Task HashSetAsync(string key, HashEntry[] entries) => Db.HashSetAsync(key, entries);
        public Task<HashEntry[]> HashGetAllAsync(string key) => Db.HashGetAllAsync(key);
        public Task<bool> SetExpiryAsync(string key, TimeSpan ttl) => Db.KeyExpireAsync(key, ttl);
        public Task<bool> SetStringIfNotExistsAsync(string key, string value, TimeSpan? expiry = null) => Db.StringSetAsync(key, value, expiry, When.NotExists);
        public Task GeoAddAsync(string key, double longitude, double latitude, string member) => Db.GeoAddAsync(key, longitude, latitude, member);
        public Task<GeoRadiusResult[]> GeoRadiusAsync(string key, double longitude, double latitude, double radiusKm) => Db.GeoRadiusAsync(key, longitude, latitude, radiusKm, GeoUnit.Kilometers);
        public async Task<string?> HashGetAsync(string key, string field)
        {
            var val = await Db.HashGetAsync(key, field);
            return val.HasValue ? val.ToString() : null;
        }
        public Task HashDeleteAsync(string key, string field) => Db.HashDeleteAsync(key, field);
        public Task<TimeSpan?> GetTTLAsync(string key) => Db.KeyTimeToLiveAsync(key);
        public Task<GeoPosition?> GeoPositionAsync(string key, string member) => Db.GeoPositionAsync(key, member);
        public Task<IEnumerable<string>> ListKeysAsync(string pattern)
        {
            var endpoint = _mux.GetEndPoints().First();
            var server = _mux.GetServer(endpoint);
            var keys = server.Keys(pattern: pattern).Select(k => k.ToString());
            return Task.FromResult(keys);
        }
    }
}
