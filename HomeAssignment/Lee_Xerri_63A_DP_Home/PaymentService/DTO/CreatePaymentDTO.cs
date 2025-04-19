using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTO
{
    public class CreatePaymentDTO
    {
        [Required] public string BookingId { get; set; }
    }
}
