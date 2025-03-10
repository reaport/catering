namespace CateringService.Models
{
    /// <summary>
    /// Ответ успешного выполнения запроса доставки питания.
    /// </summary>
    public class CateringResponse
    {
        public bool Waiting { get; set; }
        public string Status { get; set; }
    }
}
