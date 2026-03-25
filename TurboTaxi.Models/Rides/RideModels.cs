using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Models.Rides
{
    public class CreateRideRequest
    {
        public double PickupLat { get; set; }
        public double PickupLng { get; set; }
        public double? DestinationLat { get; set; }
        public double? DestinationLng { get; set; }
        public VehicleType VehicleType { get; set; } = VehicleType.Eco;
        public int UserId { get; set; }
    }

    public class CreateRideResponse
    {
        public int RideId { get; set; }
        public string Status { get; set; } = null!;
        public List<int> CandidateDriverIds { get; set; } = new();
        public decimal EstimatedFare { get; set; } // Estimated fare in AZN
        public double? DistanceKm { get; set; } // Estimated distance in kilometers
        public int? DurationMinutes { get; set; } // Estimated duration in minutes
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
        // 🔧 Optional: FinalPrice no longer used
        // System uses EstimatedFare set at ride creation time
        public decimal? FinalPrice { get; set; }
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

    // SignalR Payload for Driver - sent when new ride request arrives
    public class NewRideRequestPayload
    {
        public int RideId { get; set; }
        public string VehicleType { get; set; } = null!; // Vehicle category (Eco, Comfort, Premium)
        public double PickupLat { get; set; }
        public double PickupLng { get; set; }
        public double DestinationLat { get; set; }
        public double DestinationLng { get; set; }
        public string? EncodedPolyline { get; set; } // Google Maps encoded polyline
        public decimal EstimatedFare { get; set; } // Estimated earnings for driver
        public double? DistanceKm { get; set; } // Distance in kilometers
        public int? DurationMinutes { get; set; } // Estimated trip duration
    }

    // SignalR Payload for Ride Completion - sent to both User and Driver
    public class RideCompletionPayload
    {
        public int RideId { get; set; }
        public string Status { get; set; } = "Completed"; // RideStatus.Completed
        public decimal FinalPrice { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public bool ClearMap { get; set; } = true;
        public string? Message { get; set; }
        public string? UserType { get; set; } // "user" or "driver" - for client-side handling
    }
}
