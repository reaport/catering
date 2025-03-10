using System.Collections.Generic;

namespace CateringService.Services
{
    public interface IMealTypeService
    {
        List<string> GetMealTypes();
        void UpdateMealTypes(List<string> mealTypes);
    }
}
