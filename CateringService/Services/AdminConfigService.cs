using CateringService.Models;

namespace CateringService.Services
{
    public class AdminConfigService : IAdminConfigService
    {
        private AdminConfig _config = new AdminConfig();

        public AdminConfig GetConfig() => _config;

        public void UpdateConfig(AdminConfig config)
        {
            _config.ConflictRetryCount = config.ConflictRetryCount;
            _config.MovementSpeed = config.MovementSpeed;
            _config.NumberOfCateringVehicles = config.NumberOfCateringVehicles;
        }
    }
}
