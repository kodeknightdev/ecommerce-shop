using System.Collections.Concurrent;

namespace EcommerceShop.Payments.Providers;

// offline stand-in for Stripe, knows the same pm_card_* ids
public sealed class MockPaymentProvider : IPaymentProvider
{
    public const decimal MaxAmount = 5_000m;

    private static readonly Dictionary<string, string> Declines = new(StringComparer.Ordinal)
    {
        ["pm_card_chargeDeclined"] = "Your card was declined.",
        ["pm_card_visa_chargeDeclinedInsufficientFunds"] = "Your card has insufficient funds.",
        ["pm_card_chargeDeclinedExpiredCard"] = "Your card has expired."
    };

    // in memory only, so after a restart older charges can't be looked up
    private readonly ConcurrentDictionary<string, ProviderPayment> _ledger = new(StringComparer.Ordinal);
    private readonly TimeSpan _latency;

    public MockPaymentProvider(TimeSpan? latency = null) => _latency = latency ?? TimeSpan.FromMilliseconds(300);

    public string Name => "mock";

    public async Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        await Task.Delay(_latency, ct);
        var reference = $"mock_pi_{request.OrderId:N}";

        string? failure = Declines.TryGetValue(request.PaymentMethod, out var reason)
            ? reason
            : request.Amount > MaxAmount ? $"Amount exceeds the mock limit of {MaxAmount:0.00}." : null;

        // same reference twice = same idempotent charge, like Stripe
        var recorded = _ledger.GetOrAdd(reference, _ => new ProviderPayment(reference,
            failure is null ? ProviderPaymentStatus.Succeeded : ProviderPaymentStatus.Failed, request.Amount, request.Currency, 0));

        return recorded.Status == ProviderPaymentStatus.Succeeded ? ChargeResult.Success(reference) : ChargeResult.Failure(failure ?? "Payment failed", reference);
    }

    public Task<ProviderPayment?> LookupAsync(string providerReference, CancellationToken ct) =>
        Task.FromResult(_ledger.TryGetValue(providerReference, out var payment) ? payment : null);

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        await Task.Delay(_latency, ct);
        if (_ledger.TryGetValue(request.ProviderReference, out var payment))
        {
            if (payment.Status != ProviderPaymentStatus.Succeeded)
            {
                return new RefundResult(false, null, "Nothing was charged, so there is nothing to refund.");
            }

            _ledger[request.ProviderReference] = payment with { AmountRefunded = request.Amount };
        }

        return new RefundResult(true, $"mock_re_{request.OrderId:N}", null);
    }
}
