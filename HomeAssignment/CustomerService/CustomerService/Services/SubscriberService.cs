using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using CustomerService.Models;
using System.Text.Json;

namespace CustomerService.Services
{
    public class SubscriberService : BackgroundService
    {
        private readonly SubscriberServiceApiClient _subscriber;
        private readonly FirestoreDb _firestore;
        private readonly ILogger<SubscriberService> _logger;
        private readonly PubSubSubscriptions _subs;

        public SubscriberService(
            SubscriberServiceApiClient subscriber,
            FirestoreDb firestore,
            ILogger<SubscriberService> logger,
            PubSubSubscriptions subs)
        {
            _subscriber = subscriber;
            _firestore = firestore;
            _logger = logger;
            _subs = subs;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // fire-and-forget
            _ = Task.Run(() => PullMessagesAsync(_subs.DiscountTopicSub, "discount", stoppingToken));
            _ = Task.Run(() => PullMessagesAsync(_subs.BookingTopicSub, "booking", stoppingToken));

            return Task.CompletedTask;
        }

        private async Task PullMessagesAsync(SubscriptionName subscription, string notificationType, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _subscriber.PullAsync(subscription, returnImmediately: false, maxMessages: 10);

                    foreach (var msg in response.ReceivedMessages)
                    {
                        try
                        {
                            var payload = JsonSerializer.Deserialize<SubMessage>(
                                msg.Message.Data.ToStringUtf8());

                            if (!string.IsNullOrEmpty(payload?.Uid))
                            {
                                await _firestore.Collection("users")
                                .Document(payload.Uid)
                                .Collection("notifications")
                                .Document(notificationType == "booking" && !string.IsNullOrEmpty(payload.BookingId)
                                            ? $"Booking-{payload.BookingId}"
                                            : notificationType)
                                .SetAsync(new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    message = payload.Message,
                                    timestamp = DateTime.UtcNow
                                });

                                await _subscriber.AcknowledgeAsync(subscription, new[] { msg.AckId });

                                _logger.LogInformation("Processed {type} notification for UID {uid}.", notificationType, payload.Uid);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process {type} message.", notificationType);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
                {
                    // timeout, safe to continue
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pull messages for {type}.", notificationType);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
