using System.Collections.Generic;

namespace CateringService.Models
{
    public class AdminDashboardViewModel
    {
        public VehicleCapacity Capacity { get; set; }
        public MealTypesResponse MealTypes { get; set; }
        public List<VehicleStatusInfo> Vehicles { get; set; }
    }
}
