using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class CapacityService : ICapacityService
    {
        private int _capacity = 100;
        private readonly ILogger<CapacityService> _logger;

        public CapacityService(ILogger<CapacityService> logger)
        {
            _logger = logger;
        }

        public int GetCapacity() => _capacity;

        public void UpdateCapacity(int capacity)
        {
            if (capacity < 0) return;
            _capacity = capacity;
            _logger.LogInformation("CapacityService: Updated capacity to {Capacity}", capacity);
        }
    }
}
