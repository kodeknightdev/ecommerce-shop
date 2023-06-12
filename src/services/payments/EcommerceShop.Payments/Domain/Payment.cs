namespace EcommerceShop.Payments.Domain;

public enum PaymentStatus
{
    Succeeded,
    Failed,
    Refunded
}

// one per order (unique index on OrderId)
public sealed class Payment
{
    private Payment() { }

    public Payment(Guid orderId, Guid userId, string email, decimal amount, string currency, string provider, string idempotencyKey)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        UserId = userId;
        Email = email;
        Amount = amount;
        Currency = currency;
        Provider = provider;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string Provider { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string? RefundReference { get; private set; }
    public string? RefundReason { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    public void Succeed(string? providerReference)
    {
        Status = PaymentStatus.Succeeded;
        ProviderReference = providerReference;
        FailureReason = null;
    }

    public void Fail(string reason, string? providerReference)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        ProviderReference = providerReference;
    }

    public void MarkRefunded(string refundReference, string reason)
    {
        Status = PaymentStatus.Refunded;
        RefundReference = refundReference;
        RefundReason = reason;
        RefundedAt = DateTimeOffset.UtcNow;
    }
}

// from orders.order-cancelled, so payments knows which charges should not stand
public sealed class CancelledOrder
{
    private CancelledOrder() { }

    public CancelledOrder(Guid orderId, string reason, DateTimeOffset cancelledAt)
    {
        OrderId = orderId;
        Reason = reason;
        CancelledAt = cancelledAt;
    }

    public Guid OrderId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CancelledAt { get; private set; }
}
