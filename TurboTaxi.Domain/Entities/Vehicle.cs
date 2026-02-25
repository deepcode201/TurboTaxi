using TurboTaxi.Domain.BaseEntities;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Domain.Entities
{
    public class Vehicle : BaseEntity
    {
        public string PlateNumber { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Year { get; set; }

        public VehicleType VehicleType { get; set; } = VehicleType.Standard;

        // Navigation
        public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    }
}
