using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTO
{
    public class CreatePaymentDTO
    {
        [Required] public string BookingId { get; set; }
        public double? Discount { get; set; } = 1.0; // set as default to no discount (optional field)
    }
}
