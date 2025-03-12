using System.Collections.Generic;

namespace CateringService.Models
{
    public class AdminViewModel
    {
        public AdminConfig Config { get; set; }
        public IEnumerable<CateringVehicleInfo> Vehicles { get; set; }
        // Режим работы: "Mock" или "Real"
        public string Mode { get; set; }
        // Список типов питания для редактирования в админке
        public List<string> MealTypesList { get; set; }
    }
}
