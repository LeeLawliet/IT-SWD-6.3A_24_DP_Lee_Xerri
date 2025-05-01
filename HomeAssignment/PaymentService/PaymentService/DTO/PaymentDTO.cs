using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTO
{
    public class PaymentDTO
    {
        [Required] public string Id { get; set; }
        [Required] public string BookingId { get; set; }
        [Required] public double TotalPrice { get; set; }
        [Required] public DateTime CreatedAt { get; set; }
    }
}
