using System.Text.Json.Serialization;
using TurboTaxi.Domain.Enums;

namespace TurboTaxi.Models.Realtime
{
    public class DriverLocationUpdateRequest
    {
        public int DriverId { get; set; }

        /// <summary>
        /// Driver latitude.
        /// Handles both camelCase (latitude) from SignalR and PascalCase (Latitude) from REST.
        /// PropertyNameCaseInsensitive in Program.cs enables this mapping.
        /// NO [JsonPropertyName] here - causes deserialization issues!
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Driver longitude.
        /// Handles both camelCase (longitude) from SignalR and PascalCase (Longitude) from REST.
        /// PropertyNameCaseInsensitive in Program.cs enables this mapping.
        /// NO [JsonPropertyName] here - causes deserialization issues!
        /// </summary>
        public double Longitude { get; set; }

        // Enum ID: 0=Eco, 1=Business, 2=XL, 3=Delivery
        public VehicleType VehicleType { get; set; } = VehicleType.Eco;

        // Enum ID: 0=Offline, 1=Free, 2=Busy, 3=BusySoon
        // Accepts: statusOptional (camelCase from frontend) or DriverStatus (PascalCase)
        [JsonPropertyName("statusOptional")]
        public DriverStatus? DriverStatus { get; set; }
    }
}
