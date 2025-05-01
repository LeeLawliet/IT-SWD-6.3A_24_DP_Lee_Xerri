using Google.Cloud.PubSub.V1;

namespace CustomerService.Models
{
    public class SubMessage
    {
        public string Uid { get; set; }
        public string? BookingId { get; set; }
        public string Message { get; set; }
    }
}
