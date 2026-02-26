namespace TurboTaxi.Models.Routes
{
    public class RouteEstimateRequest
    {
        public Coordinate Origin { get; set; } = new();
        public Coordinate Destination { get; set; } = new();
        public string VehicleType { get; set; } = "Standard";
        public string? PromoCode { get; set; }
    }

    public class Coordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class RouteEstimateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public RouteEstimateData? Data { get; set; }
    }

    public class RouteEstimateData
    {
        public double DistanceKm { get; set; }
        public int DurationMinutes { get; set; }
        public RoutePriceDto Price { get; set; } = new();
        public string? EncodedPolyline { get; set; }
    }

    public class RoutePriceDto
    {
        public double Original { get; set; }
        public double Discount { get; set; }
        public double Final { get; set; }
        public string Currency { get; set; } = "AZN";
    }
}
