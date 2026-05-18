namespace TurboTaxi.Application.Helpers
{
    public static class RideIdParser
    {
        public static int Parse(string rideIdOrRequestId)
        {
            if (string.IsNullOrWhiteSpace(rideIdOrRequestId))
                throw new ArgumentException("Ride id is required.");

            var s = rideIdOrRequestId.Trim();
            if (s.StartsWith("ride_", StringComparison.OrdinalIgnoreCase))
                s = s[5..];

            if (!int.TryParse(s, out var id) || id <= 0)
                throw new ArgumentException($"Invalid ride id: '{rideIdOrRequestId}'");

            return id;
        }
    }
}
