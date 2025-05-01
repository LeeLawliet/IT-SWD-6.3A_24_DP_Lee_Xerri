namespace WebApp.Models
{
    public class BookingDTO
    {
        public string Id { get; set; } = "";
        public string StartLocation { get; set; } = "";
        public string EndLocation { get; set; } = "";
        public DateTime DateTime { get; set; }
        public int Passengers { get; set; }
        public string CabType { get; set; } = "";
        public bool Paid { get; set; }
    }

}
