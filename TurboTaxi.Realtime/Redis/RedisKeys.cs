namespace TurboTaxi.Realtime.Redis
{
    public static class RedisKeys
    {
        public static string DriverHash(int driverId) => $"driver:{driverId}:hash";
        public static string DriverGeo(string vehicleType) => $"geo:drivers:{vehicleType.ToLower()}";
        public static string DriverHeartbeat(int driverId) => $"driver:{driverId}:heartbeat";
        public static string PendingRide(string requestId) => $"ride:pending:{requestId}";
    }
}
