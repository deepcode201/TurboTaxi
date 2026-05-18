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
        /// <summary>Fare after promo (what user pays).</summary>
        public decimal EstimatedFare { get; set; }
        public decimal OriginalEstimatedFare { get; set; }
        public decimal? DiscountAmount { get; set; }
        public bool PromoApplied { get; set; }
        public string? PromoCode { get; set; }
        public int? PromoRemainingRides { get; set; }
        public double? DistanceKm { get; set; }
        public int? DurationMinutes { get; set; }
    }

    public class AcceptRideRequest
    {
        /// <summary>Ride id (if not in URL). Also accepts SignalR requestId in RequestId.</summary>
        public int RideId { get; set; }
        /// <summary>SignalR first arg e.g. "ride_123".</summary>
        public string? RequestId { get; set; }
        public int DriverId { get; set; }
        /// <summary>Optional — used when Redis has no recent driver location.</summary>
        public double? DriverLat { get; set; }
        public double? DriverLng { get; set; }
    }

    public class RideAcceptedResponse
    {
        public bool Success { get; set; } = true;
        public int RideId { get; set; }
        public int DriverId { get; set; }
        public string Status { get; set; } = null!;
        public string? EncodedPolyline { get; set; }
        public string? TripEncodedPolyline { get; set; }
        public string? PolylinePhase { get; set; }
        public double? DistanceKm { get; set; }
        public int? DurationMinutes { get; set; }
        public double PickupLat { get; set; }
        public double PickupLng { get; set; }
        public double DestinationLat { get; set; }
        public double DestinationLng { get; set; }
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
        public decimal OriginalFare { get; set; }
        public decimal? DiscountAmount { get; set; }
        public bool PromoApplied { get; set; }
        public string? PromoCode { get; set; }
        public int? PromoRemainingRides { get; set; }
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

    /// <summary>SignalR payload — driver should remove ride from pending list.</summary>
    public class RideDismissedPayload
    {
        public int RideId { get; set; }
        /// <summary>Canceled | Taken</summary>
        public string Reason { get; set; } = null!;
        public string Status { get; set; } = "Canceled";
        public string? Message { get; set; }
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
        public string? EncodedPolyline { get; set; } // Google Maps encoded polyline
        /// <summary>Pickup -> destination route (shown while waiting for driver).</summary>
        public string? TripEncodedPolyline { get; set; }
        /// <summary>Approach | Trip | EnRoute — which polyline is in EncodedPolyline.</summary>
        public string? PolylinePhase { get; set; }
        public double? DistanceKm { get; set; } // Distance in kilometers
        public int? DurationMinutes { get; set; } // Estimated trip duration
    }
}
