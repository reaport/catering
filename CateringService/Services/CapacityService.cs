using CateringService.Models;
using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class CapacityService : ICapacityService
    {
        private VehicleCapacity _currentCapacity = new VehicleCapacity { Capacity = 100 };
        private readonly ILogger<CapacityService> _logger;

        public CapacityService(ILogger<CapacityService> logger)
        {
            _logger = logger;
        }

        public VehicleCapacity GetCapacity() => _currentCapacity;

        public void UpdateCapacity(VehicleCapacity capacity)
        {
            if (capacity != null && capacity.Capacity >= 0)
            {
                _currentCapacity = capacity;
                _logger.LogInformation("CapacityService: Updated capacity to {Capacity}", capacity.Capacity);
            }
        }
    }
}
