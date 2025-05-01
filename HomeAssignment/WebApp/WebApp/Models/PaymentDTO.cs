namespace WebApp.Models
{
    public class PaymentDTO
    {
        public string Id { get; set; } = "";
        public string BookingId { get; set; } = "";
        public double TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
