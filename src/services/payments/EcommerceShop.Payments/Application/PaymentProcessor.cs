using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Contracts;
using EcommerceShop.Payments.Data;
using EcommerceShop.Payments.Domain;
using EcommerceShop.Payments.Providers;
using Microsoft.EntityFrameworkCore;

namespace EcommerceShop.Payments.Application;

// payment, inbox row and outcome event are saved in one transaction; the inbox, the unique
// OrderId index and the Stripe idempotency key stop a double charge
public sealed class PaymentProcessor
{
    private readonly PaymentsDbContext _db;
    private readonly IPaymentProvider _provider;
    private readonly ILogger<PaymentProcessor> _logger;

    public PaymentProcessor(PaymentsDbContext db, IPaymentProvider provider, ILogger<PaymentProcessor> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    // order id = Stripe idempotency key
    public static string IdempotencyKeyFor(Guid orderId) => orderId.ToString();

    public async Task<Payment?> HandleStockReservedAsync(Guid eventId, StockReserved e, CancellationToken ct)
    {
        if (await _db.AlreadyProcessedAsync(eventId, ct))
        {
            _logger.LogInformation("Skipping duplicate StockReserved {EventId}", eventId);
            return null;
        }

        var payment = await _db.Payments.SingleOrDefaultAsync(p => p.OrderId == e.OrderId, ct);
        if (payment is null && await _db.CancelledOrders.AnyAsync(c => c.OrderId == e.OrderId, ct))
        {
            // e.g. the reservation expired while we were down; charging now would only mean a refund later
            _logger.LogInformation("Order {OrderId} is already cancelled; not charging", e.OrderId);
            _db.MarkProcessed(eventId, EventTypes.StockReserved);
            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (payment is not null)
        {
            // already charged (e.g. inventory answered twice) - just re-announce the outcome
            _logger.LogInformation("Order {OrderId} already has payment {PaymentId}; re-announcing outcome", e.OrderId, payment.Id);
        }
        else
        {
            payment = new Payment(e.OrderId, e.UserId, e.Email, e.Total, e.Currency, _provider.Name, IdempotencyKeyFor(e.OrderId));
            var result = await _provider.ChargeAsync(new ChargeRequest(e.OrderId, e.Total, e.Currency, e.PaymentMethod, e.Email, payment.IdempotencyKey), ct);

            if (result.Succeeded)
            {
                payment.Succeed(result.ProviderReference);
            }
            else
            {
                payment.Fail(result.FailureReason ?? "Payment failed", result.ProviderReference);
            }

            _db.Payments.Add(payment);
            _logger.LogInformation("Payment {PaymentId} for order {OrderId}: {Status} via {Provider}", payment.Id, e.OrderId, payment.Status, payment.Provider);
        }

        EnqueueOutcome(payment);
        _db.MarkProcessed(eventId, EventTypes.StockReserved);
        await _db.SaveChangesAsync(ct);
        return payment;
    }

    // remembered so reconciliation can refund a charge that raced the cancellation
    public async Task HandleOrderCancelledAsync(Guid eventId, OrderCancelled e, DateTimeOffset occurredAt, CancellationToken ct)
    {
        if (await _db.AlreadyProcessedAsync(eventId, ct))
        {
            return;
        }

        if (!await _db.CancelledOrders.AnyAsync(c => c.OrderId == e.OrderId, ct))
        {
            _db.CancelledOrders.Add(new CancelledOrder(e.OrderId, e.Reason, occurredAt));
        }

        _db.MarkProcessed(eventId, EventTypes.OrderCancelled);
        await _db.SaveChangesAsync(ct);
    }

    private void EnqueueOutcome(Payment payment)
    {
        if (payment.Status == PaymentStatus.Succeeded)
        {
            _db.Enqueue(Topics.PaymentSucceeded, payment.OrderId, EventTypes.PaymentSucceeded,
                new PaymentSucceeded(payment.OrderId, payment.Id, payment.Amount, payment.Currency, payment.Provider, payment.ProviderReference));
        }
        else
        {
            _db.Enqueue(Topics.PaymentFailed, payment.OrderId, EventTypes.PaymentFailed,
                new PaymentFailed(payment.OrderId, payment.Id, payment.UserId, payment.Email, payment.Amount, payment.FailureReason ?? "Payment failed"));
        }
    }
}
