using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Models.Vehicles
{
    public class VehicleCreateRequest
    {
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public VehicleType VehicleType { get; set; }
    }
    public class VehicleUpdateRequest
    {
        public int Id { get; set; }
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public VehicleType VehicleType { get; set; }
    }
    public class VehicleDto
    {
        public int Id { get; set; }
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public VehicleType VehicleType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
