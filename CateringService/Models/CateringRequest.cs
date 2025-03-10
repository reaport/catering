using System.Collections.Generic;

namespace CateringService.Models
{
    /// <summary>
    /// Модель запроса доставки питания.
    /// AircraftId – идентификатор самолёта, для которого осуществляется доставка.
    /// NodeId – идентификатор точки доставки (опционально).
    /// Meals – список заказов питания.
    /// </summary>
    public class CateringRequest
    {
        public string AircraftId { get; set; }
        public string NodeId { get; set; }
        public List<MealOrder> Meals { get; set; }
    }
}
