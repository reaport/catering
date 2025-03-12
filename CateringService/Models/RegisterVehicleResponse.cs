using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CateringService.Models
{
    public class RegisterVehicleResponse
    {
        //используем атрибут для привязки
        [JsonPropertyName("GarrageNodeId")]
        public string GarageNodeId { get; set; }
        public string VehicleId { get; set; }
        public Dictionary<string, string> ServiceSpots { get; set; }
    }
}
