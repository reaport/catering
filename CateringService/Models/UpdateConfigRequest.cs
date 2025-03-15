namespace CateringService.Models
{
    public class UpdateConfigRequest
    {
        public int ConflictRetryCount { get; set; }
        public double MovementSpeed { get; set; }
        public int NumberOfCateringVehicles { get; set; }
    }
}
