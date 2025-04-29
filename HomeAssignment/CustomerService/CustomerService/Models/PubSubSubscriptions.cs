using Google.Cloud.PubSub.V1;

namespace CustomerService.Models
{
    public class PubSubSubscriptions
    {
        public SubscriptionName DiscountTopicSub { get; set; }
        public SubscriptionName BookingTopicSub { get; set; }
    }
}
