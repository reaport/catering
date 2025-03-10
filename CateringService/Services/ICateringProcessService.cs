using System.Threading.Tasks;
using CateringService.Models;

namespace CateringService.Services
{
    public interface ICateringProcessService
    {
        Task<CateringResponse> ProcessCateringRequest(CateringRequest request);
    }
}
