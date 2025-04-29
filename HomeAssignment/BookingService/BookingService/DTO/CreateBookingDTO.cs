using System.ComponentModel.DataAnnotations;

namespace BookingService.DTO
{
    public class CreateBookingDTO
    {
        [Required] public string StartLocation { get; set; }
        [Required] public string EndLocation { get; set; }
        [Required] public int Passengers { get; set; }
        [Required] public string CabType { get; set; }
    }
}
