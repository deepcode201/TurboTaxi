using System.Text.Json.Serialization;

namespace TurboTaxi.Models.Realtime
{
    public class DriverLocationUpdateRequest
    {
        public int DriverId { get; set; }
        
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("lng")]
        public double Longitude { get; set; }
        
        public string VehicleType { get; set; } = "Standard";
        
        [JsonPropertyName("statusOptional")]
        public string? DriverStatus { get; set; }
    }
}
