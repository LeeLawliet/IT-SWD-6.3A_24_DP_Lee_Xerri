using System.ComponentModel.DataAnnotations;

namespace WebApp.Models
{
    public class CreateBookingDTO
    {
        public string StartLocation { get; set; } = "";
        public string EndLocation { get; set; } = "";

        [Range(1, 8, ErrorMessage = "Passenger count must be between 1 and 8.")]
        public int Passengers { get; set; }

        [Required(ErrorMessage = "Cab type is required.")]
        public string CabType { get; set; } = "";
    }
}
