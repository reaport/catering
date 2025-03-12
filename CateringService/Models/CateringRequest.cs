using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CateringService.Models
{
    public class CateringRequest
    {
        [Required]
        public string AircraftId { get; set; }
        public string NodeId { get; set; }
        [Required]
        public List<MealOrder> Meals { get; set; }
    }
}
