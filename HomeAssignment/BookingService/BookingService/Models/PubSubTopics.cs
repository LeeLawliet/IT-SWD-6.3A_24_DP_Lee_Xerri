using Google.Cloud.PubSub.V1;

namespace BookingService.Models
{
    public class PubSubTopics
    {
        public TopicName DiscountTopic { get; set; }
        public TopicName BookingTopic { get; set; }
    }
}
