using CateringService.Models;

namespace CateringService.Services
{
    public interface IAdminConfigService
    {
        AdminConfig GetConfig();
        void UpdateConfig(AdminConfig config);
    }
}
