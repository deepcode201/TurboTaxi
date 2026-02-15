namespace TurboTaxi.Models.Rides
{
    public class CreateRideRequest
    {
        public double PickupLat { get; set; }
        public double PickupLng { get; set; }
        public double? DestinationLat { get; set; }
        public double? DestinationLng { get; set; }
        public string VehicleType { get; set; } = "Standard";
        public int UserId { get; set; }
    }

    public class CreateRideResponse
    {
        public int RideId { get; set; }
        public string Status { get; set; } = null!;
        public List<int> CandidateDriverIds { get; set; } = new();
    }

    public class AcceptRideRequest
    {
        public int DriverId { get; set; }
    }

    public class RideAcceptedResponse
    {
        public int RideId { get; set; }
        public int DriverId { get; set; }
        public string Status { get; set; } = null!;
    }

    public class StartRideRequest
    {
        public int DriverId { get; set; }
    }

    public class StartRideResponse
    {
        public int RideId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime StartedAtUtc { get; set; }
    }

    public class FinishRideRequest
    {
        public int DriverId { get; set; }
        public decimal FinalPrice { get; set; }
    }

    public class RideFinishedResponse
    {
        public int RideId { get; set; }
        public string Status { get; set; } = null!;
        public decimal FinalPrice { get; set; }
        public DateTime FinishedAtUtc { get; set; }
    }

    public class CancelRideByUserRequest
    {
        public int UserId { get; set; }
    }

    public class CancelRideByDriverRequest
    {
        public int DriverId { get; set; }
    }

    public class CancelRideResponse
    {
        public int RideId { get; set; }
        public string Status { get; set; } = null!;
    }
}
