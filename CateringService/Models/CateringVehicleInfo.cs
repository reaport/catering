namespace CateringService.Models
{
    public class CateringVehicleInfo
    {
        public string VehicleId { get; set; }
        public string Status { get; set; }
        // Текущее местоположение (например, гараж или узел доставки)
        public string CurrentNode { get; set; }
    }
}
