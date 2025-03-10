using System.Collections.Generic;

namespace CateringService.Models
{
    /// <summary>
    /// Модель ответа для получения списка доступных типов питания.
    /// </summary>
    public class MealTypesResponse
    {
        /// <summary>
        /// Список доступных типов питания.
        /// </summary>
        public List<string> MealTypes { get; set; }
    }
}
