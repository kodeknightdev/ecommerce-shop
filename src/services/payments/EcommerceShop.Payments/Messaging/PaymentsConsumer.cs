using EcommerceShop.BuildingBlocks.Kafka;
using EcommerceShop.Contracts;
using EcommerceShop.Payments.Application;
using Microsoft.Extensions.Options;

namespace EcommerceShop.Payments.Messaging;

public sealed class PaymentsConsumer : KafkaConsumerService
{
    private static readonly string[] Subscriptions = { Topics.StockReserved, Topics.OrderCancelled };

    public PaymentsConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> options, ILogger<PaymentsConsumer> logger)
        : base(scopeFactory, options, logger) { }

    protected override IReadOnlyCollection<string> SubscribedTopics => Subscriptions;

    protected override async Task HandleAsync(IServiceProvider services, string topic, RawEnvelope envelope, CancellationToken ct)
    {
        if (topic == Topics.StockReserved)
        {
            await services.GetRequiredService<PaymentProcessor>().HandleStockReservedAsync(envelope.EventId, Payload<StockReserved>(envelope), ct);
        }
        else if (topic == Topics.OrderCancelled)
        {
            await services.GetRequiredService<PaymentProcessor>()
                .HandleOrderCancelledAsync(envelope.EventId, Payload<OrderCancelled>(envelope), envelope.OccurredAt, ct);
        }
    }
}
