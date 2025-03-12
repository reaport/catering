using System.Collections.Generic;
using System.Threading.Tasks;
using CateringService.Models;

namespace CateringService.Services
{
    public interface IExternalApiService
    {
        Task<RegisterVehicleResponse> RegisterVehicleAsync(string type);
        Task<List<string>> GetRouteAsync(string from, string to, string vehicleType);
        Task<double> RequestMoveAsync(string vehicleId, string vehicleType, string from, string to);
        Task NotifyArrivalAsync(string vehicleId, string vehicleType, string nodeId);
        Task NotifyCateringStartAsync(string flightId);
        Task NotifyCateringFinishAsync(string flightId, int totalMeals);
    }
}
