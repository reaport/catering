using CateringService.Models;

namespace CateringService.Services
{
    public interface ICapacityService
    {
        VehicleCapacity GetCapacity();
        void UpdateCapacity(VehicleCapacity capacity);
    }
}
