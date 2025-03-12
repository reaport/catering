namespace CateringService.Models
{
    public class AdminConfig
    {
        public int ConflictRetryCount { get; set; } = 30;
        public double MovementSpeed { get; set; } = 10.0;
        public int NumberOfCateringVehicles { get; set; } = 5;
    }
}
