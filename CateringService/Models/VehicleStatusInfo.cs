using System.Collections.Generic;

namespace CateringService.Models
{
    public class VehicleStatusInfo
    {
        public string VehicleId { get; set; }
        public string BaseNode { get; set; }
        public string Status { get; set; } // "Busy" или "Available"
        public Dictionary<string, string> ServiceSpots { get; set; }
    }
}
