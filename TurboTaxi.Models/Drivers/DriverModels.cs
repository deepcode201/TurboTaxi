namespace TurboTaxi.Models.Drivers
{
    public class DriverCreateRequest
    {
        public int UserId { get; set; }
        public bool IsVerified { get; set; } = false;
        public int? VehicleId { get; set; }
    }

    public class DriverUpdateRequest
    {
        public int Id { get; set; }
        public bool? IsVerified { get; set; }
        public int? VehicleId { get; set; }
    }

    public class DriverDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public bool IsVerified { get; set; }
        public int? VehicleId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
