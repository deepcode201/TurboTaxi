using System.Text.Json.Serialization;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Models.Realtime
{
    public class DriverLocationUpdateRequest
    {
        public int DriverId { get; set; }
        
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("lng")]
        public double Longitude { get; set; }

        // Enum ID: 0=Eco, 1=Business, 2=XL, 3=Delivery
        public VehicleType VehicleType { get; set; } = VehicleType.Eco;
        
        // Enum ID: 0=Offline, 1=Free, 2=Busy, 3=BusySoon
        [JsonPropertyName("statusOptional")]
        public DriverStatus? DriverStatus { get; set; }
    }
}
