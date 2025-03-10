namespace CateringService.Models
{
    /// <summary>
    /// Модель заказа питания.
    /// </summary>
    public class MealOrder
    {
        /// <summary>
        /// Тип питания (например, "Standard", "Vegetarian", "Vegan", "Gluten-Free").
        /// </summary>
        public string MealType { get; set; }

        /// <summary>
        /// Количество порций для данного типа питания.
        /// </summary>
        public int Count { get; set; }
    }
}
