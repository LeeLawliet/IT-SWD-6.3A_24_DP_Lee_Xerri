namespace PaymentService.DTO
{
    public class BookingHttpDTO
    {
        public string Id { get; set; } = default!;
        public string StartLocation { get; set; } = default!;
        public string EndLocation { get; set; } = default!;
        public DateTime DateTime { get; set; }
        public int Passengers { get; set; }
        public string CabType { get; set; } = default!;
        public bool Paid { get; set; }
    }
}
