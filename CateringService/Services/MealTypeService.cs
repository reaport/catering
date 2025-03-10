using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class MealTypeService : IMealTypeService
    {
        private List<string> _mealTypes = new List<string> { "Standard", "Vegetarian", "Vegan", "Gluten-Free" };
        private readonly ILogger<MealTypeService> _logger;

        public MealTypeService(ILogger<MealTypeService> logger)
        {
            _logger = logger;
        }

        public List<string> GetMealTypes()
        {
            _logger.LogInformation("MealTypeService: returning meal types");
            return _mealTypes;
        }

        public void UpdateMealTypes(List<string> mealTypes)
        {
            _mealTypes = mealTypes;
            _logger.LogInformation("MealTypeService: updated meal types to {MealTypes}", string.Join(", ", mealTypes));
        }
    }
}
