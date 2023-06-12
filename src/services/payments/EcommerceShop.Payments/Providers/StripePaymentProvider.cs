using Stripe;

namespace EcommerceShop.Payments.Providers;

// test mode only - live keys are refused at startup
public sealed class StripePaymentProvider : IPaymentProvider
{
    private readonly IStripeClient _client;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(IStripeClient client, ILogger<StripePaymentProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "stripe";

    public async Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        var service = new PaymentIntentService(_client);
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
            Currency = request.Currency.ToLowerInvariant(),
            PaymentMethod = request.PaymentMethod,
            PaymentMethodTypes = new List<string> { "card" },
            Confirm = true,
            ReceiptEmail = request.Email,
            Description = $"ecommerce-shop order {request.OrderId}",
            Metadata = new Dictionary<string, string> { ["orderId"] = request.OrderId.ToString() }
        };

        try
        {
            var intent = await service.CreateAsync(options, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, ct);
            return intent.Status == "succeeded"
                ? ChargeResult.Success(intent.Id)
                : ChargeResult.Failure($"PaymentIntent ended in status '{intent.Status}'.", intent.Id);
        }
        catch (StripeException ex) when (ex.StripeError?.Type is "card_error" or "invalid_request_error")
        {
            _logger.LogInformation("Stripe declined order {OrderId}: {Code} {Message}", request.OrderId, ex.StripeError.Code, ex.StripeError.Message);
            return ChargeResult.Failure(ex.StripeError.Message ?? "Payment was declined.", ex.StripeError.PaymentIntent?.Id);
        }
    }

    public async Task<ProviderPayment?> LookupAsync(string providerReference, CancellationToken ct)
    {
        var service = new PaymentIntentService(_client);
        try
        {
            var intent = await service.GetAsync(providerReference, new PaymentIntentGetOptions { Expand = new List<string> { "latest_charge" } },
                cancellationToken: ct);
            var status = intent.Status switch
            {
                "succeeded" => ProviderPaymentStatus.Succeeded,
                "canceled" or "requires_payment_method" => ProviderPaymentStatus.Failed,
                _ => ProviderPaymentStatus.Pending
            };
            return new ProviderPayment(intent.Id, status, intent.Amount / 100m, intent.Currency.ToUpperInvariant(),
                (intent.LatestCharge?.AmountRefunded ?? 0) / 100m);
        }
        catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new ProviderPayment(providerReference, ProviderPaymentStatus.NotFound, 0, string.Empty, 0);
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        var service = new RefundService(_client);
        var options = new RefundCreateOptions
        {
            PaymentIntent = request.ProviderReference,
            Amount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
            Reason = "requested_by_customer",
            Metadata = new Dictionary<string, string> { ["orderId"] = request.OrderId.ToString(), ["reason"] = request.Reason }
        };

        try
        {
            var refund = await service.CreateAsync(options, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, ct);
            return refund.Status is "succeeded" or "pending"
                ? new RefundResult(true, refund.Id, null)
                : new RefundResult(false, refund.Id, $"Refund ended in status '{refund.Status}'.");
        }
        catch (StripeException ex) when (ex.StripeError?.Type is "invalid_request_error")
        {
            _logger.LogWarning("Stripe refused the refund for order {OrderId}: {Message}", request.OrderId, ex.StripeError.Message);
            return new RefundResult(false, null, ex.StripeError.Message);
        }
    }
}

public static class PaymentProviderRegistration
{
    public static IServiceCollection AddPaymentProvider(this IServiceCollection services, IConfiguration configuration, ILogger logger)
    {
        var key = configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>()?.SecretKey?.Trim();

        if (string.IsNullOrEmpty(key) || key.Contains("xxxx", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("No Stripe test key configured - using the mock payment provider");
            services.AddSingleton<IPaymentProvider>(_ => new MockPaymentProvider());
            return services;
        }

        if (!key.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only Stripe test-mode secret keys (sk_test_...) are allowed in ecommerce-shop.");
        }

        logger.LogInformation("Using Stripe in test mode");
        services.AddSingleton<IStripeClient>(_ => new StripeClient(key));
        services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
        return services;
    }
}
