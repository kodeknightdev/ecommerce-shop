using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EcommerceShop.BuildingBlocks.Kafka;
using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Catalog.Data;
using EcommerceShop.Catalog.Domain;
using EcommerceShop.Contracts;

namespace EcommerceShop.Catalog.Messaging;

public sealed class CatalogConsumer : KafkaConsumerService
{
    private static readonly string[] Subscriptions = { Topics.OrderConfirmed };

    public CatalogConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> options, ILogger<CatalogConsumer> logger)
        : base(scopeFactory, options, logger) { }

    protected override IReadOnlyCollection<string> SubscribedTopics => Subscriptions;

    protected override async Task HandleAsync(IServiceProvider services, string topic, RawEnvelope envelope, CancellationToken ct)
    {
        if (topic == Topics.OrderConfirmed)
        {
            var db = services.GetRequiredService<CatalogDbContext>();
            await PurchaseRecorder.RecordAsync(db, envelope.EventId, Payload<OrderConfirmed>(envelope), envelope.OccurredAt, Logger, ct);
        }
    }
}

public static class PurchaseRecorder
{
    // a confirmed order is what makes someone a verified buyer
    public static async Task<int> RecordAsync(CatalogDbContext db, Guid eventId, OrderConfirmed e, DateTimeOffset occurredAt, ILogger logger,
        CancellationToken ct)
    {
        if (await db.AlreadyProcessedAsync(eventId, ct))
        {
            logger.LogInformation("Skipping duplicate OrderConfirmed {EventId}", eventId);
            return 0;
        }

        var productIds = (e.Items ?? Array.Empty<OrderLineDto>()).Select(i => i.ProductId).Distinct().ToList();
        var known = await db.VerifiedPurchases.Where(v => v.UserId == e.UserId && productIds.Contains(v.ProductId))
            .Select(v => v.ProductId).ToListAsync(ct);

        var added = 0;
        foreach (var productId in productIds.Except(known))
        {
            db.VerifiedPurchases.Add(new VerifiedPurchase(e.UserId, productId, e.OrderId, occurredAt));
            added++;
        }

        db.MarkProcessed(eventId, EventTypes.OrderConfirmed);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
