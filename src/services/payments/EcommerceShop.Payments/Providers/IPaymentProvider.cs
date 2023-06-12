namespace EcommerceShop.Payments.Providers;

public sealed record ChargeRequest(Guid OrderId, decimal Amount, string Currency, string PaymentMethod, string Email, string IdempotencyKey);

public sealed record ChargeResult(bool Succeeded, string? ProviderReference, string? FailureReason)
{
    public static ChargeResult Success(string reference) => new(true, reference, null);
    public static ChargeResult Failure(string reason, string? reference = null) => new(false, reference, reason);
}

public enum ProviderPaymentStatus
{
    Succeeded,
    Failed,
    Pending,
    NotFound
}

// what the provider itself has on record for a charge
public sealed record ProviderPayment(string Reference, ProviderPaymentStatus Status, decimal Amount, string Currency, decimal AmountRefunded);

public sealed record RefundRequest(Guid OrderId, string ProviderReference, decimal Amount, string Reason, string IdempotencyKey);

public sealed record RefundResult(bool Succeeded, string? RefundReference, string? FailureReason);

public interface IPaymentProvider
{
    string Name { get; }
    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);

    // null when the provider can't say either way
    Task<ProviderPayment?> LookupAsync(string providerReference, CancellationToken ct);

    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct);
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    // sk_test_... only; empty = mock provider
    public string? SecretKey { get; set; }
}
