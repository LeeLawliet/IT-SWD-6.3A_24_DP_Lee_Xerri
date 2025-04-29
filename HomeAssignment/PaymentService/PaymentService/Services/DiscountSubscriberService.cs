using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using PaymentService.Models;
using System.Text.Json;

namespace PaymentService.Services
{
    public class DiscountSubscriberService : BackgroundService
    {
        private readonly SubscriberServiceApiClient _subscriber;
        private readonly SubscriptionName _subscriptionName;
        private readonly FirestoreDb _firestore;
        private readonly ILogger<DiscountSubscriberService> _logger;

        public DiscountSubscriberService(
            SubscriberServiceApiClient subscriber,
            FirestoreDb firestore,
            ILogger<DiscountSubscriberService> logger,
            IConfiguration config)
        {
            _subscriber = subscriber;
            _firestore = firestore;
            _logger = logger;

            var projectId = config["PubSub:ProjectId"];
            var subscriptionId = config["PubSub:DiscountTopicId"];
            _subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var response = await _subscriber.PullAsync(_subscriptionName, returnImmediately: false, maxMessages: 10);

                        foreach (var msg in response.ReceivedMessages)
                        {
                            try
                            {
                                var payload = JsonSerializer.Deserialize<DiscountMessage>(
                                    msg.Message.Data.ToStringUtf8());

                                if (!string.IsNullOrEmpty(payload?.Uid))
                                {
                                    await _firestore.Collection("users")
                                        .Document(payload.Uid)
                                        .Collection("notifications")
                                        .Document("discount")
                                        .SetAsync(new
                                        {
                                            message = payload.Message,
                                            timestamp = Timestamp.GetCurrentTimestamp()
                                        });

                                    await _subscriber.AcknowledgeAsync(_subscriptionName, new[] { msg.AckId });

                                    _logger.LogInformation("Processed discount notification for UID {uid}.", payload.Uid);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to process discount message.");
                            }
                        }
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
                    {
                        // timeout, safe to continue
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to pull discount messages.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }, stoppingToken);
        }
    }
}