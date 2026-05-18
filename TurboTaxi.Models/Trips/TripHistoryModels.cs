namespace TurboTaxi.Models.Trips
{
    public class TripHistoryQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class UserTripHistoryItem
    {
        public int RideId { get; set; }
        public string? DriverName { get; set; }
        public string? DriverSurname { get; set; }
        public string? DriverPhoneNumber { get; set; }
        public string? DriverEmail { get; set; }
        public string? VehiclePlateNumber { get; set; }
        public string? VehicleType { get; set; }
        public string? VehicleBrand { get; set; }
        public string? VehicleModel { get; set; }
        public string? PickupAddress { get; set; }
        public string? DestinationAddress { get; set; }
        public decimal Fare { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? FinalFare { get; set; }
        public DateTime? RideDateUtc { get; set; }
        public string Status { get; set; } = null!;
    }

    public class DriverTripHistoryItem
    {
        public int RideId { get; set; }
        public string? UserName { get; set; }
        public string? UserSurname { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? UserEmail { get; set; }
        public string? PickupAddress { get; set; }
        public string? DestinationAddress { get; set; }
        public DateTime? RideDateUtc { get; set; }
        public string Status { get; set; } = null!;
    }
}
