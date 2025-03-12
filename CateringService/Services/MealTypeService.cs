using System.Collections.Generic;
using Serilog;
using ILogger = Serilog.ILogger;

namespace CateringService.Services
{
    public class MealTypeService : IMealTypeService
    {
        private List<string> _mealTypes = new List<string> { "Standard", "Vegetarian", "Vegan", "Gluten-Free" };
        private readonly ILogger _logger;

        public MealTypeService()
        {
            _logger = Log.ForContext<MealTypeService>();
        }

        public List<string> GetMealTypes()
        {
            _logger.Information("Returning meal types: {MealTypes}", string.Join(", ", _mealTypes));
            return _mealTypes;
        }

        public void UpdateMealTypes(List<string> mealTypes)
        {
            _mealTypes = mealTypes;
            _logger.Information("Updated meal types: {MealTypes}", string.Join(", ", mealTypes));
        }
    }
}
