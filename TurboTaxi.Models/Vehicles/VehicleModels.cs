namespace TurboTaxi.Models.Vehicles
{
    public class VehicleCreateRequest
    {
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public string VehicleType { get; set; } = null!; // enum name
    }
    public class VehicleUpdateRequest
    {
        public int Id { get; set; }
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public string VehicleType { get; set; } = null!;
    }
    public class VehicleDto
    {
        public int Id { get; set; }
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }
        public string VehicleType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
