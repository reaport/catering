namespace CateringService.Models
{
    public class UpdateConfigRequest
    {
        public int ConflictRetryCount { get; set; }
        public int NumberOfCateringVehicles { get; set; }
    }
}
