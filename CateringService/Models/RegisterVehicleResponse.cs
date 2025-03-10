using System.Collections.Generic;

namespace CateringService.Models
{
    /// <summary>
    /// Ответ внешнего сервиса при регистрации транспортного средства.
    /// </summary>
    public class RegisterVehicleResponse
    {
        public string GarageNodeId { get; set; }
        public string VehicleId { get; set; }
        public Dictionary<string, string> ServiceSpots { get; set; }
    }
}
