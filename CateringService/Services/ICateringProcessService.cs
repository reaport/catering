using System.Collections.Generic;
using System.Threading.Tasks;
using CateringService.Models;

namespace CateringService.Services
{
    public interface ICateringProcessService
    {
        Task<CateringResponse> ProcessCateringRequest(CateringRequest request);
        Task<bool> RegisterVehicleAsync(string type);
        Task InitializeVehiclesAsync();
        Task ReloadAsync();
        IEnumerable<CateringVehicleInfo> GetVehiclesInfo();
    }
}
