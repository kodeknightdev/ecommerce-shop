using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Contracts;
using EcommerceShop.Payments.Data;
using EcommerceShop.Payments.Domain;
using EcommerceShop.Payments.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcommerceShop.Payments.Application;

public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 15;
    public int LookbackDays { get; set; } = 7;
}

/// <summary>Compares our payments with the provider and with cancelled orders; refunds what should not have been charged.</summary>
public sealed class ReconciliationService
{
    // any constant works, it only has to be the same in every replica
    private const long LockKey = 7_301_406_120;

    private readonly PaymentsDbContext _db;
    private readonly IPaymentProvider _provider;
    private readonly ReconciliationOptions _options;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(PaymentsDbContext db, IPaymentProvider provider, IOptions<ReconciliationOptions> options,
        ILogger<ReconciliationService> logger)
    {
        _db = db;
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public static string RefundKeyFor(Guid orderId) => $"refund-{orderId}";

    // null when another replica is already running one
    public async Task<ReconciliationRun?> RunAsync(string trigger, CancellationToken ct)
    {
        var relational = _db.Database.IsRelational();
        if (relational)
        {
            await _db.Database.OpenConnectionAsync(ct);
            var locked = await _db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", LockKey).SingleAsync(ct);
            if (!locked)
            {
                await _db.Database.CloseConnectionAsync();
                _logger.LogInformation("Reconciliation already running elsewhere; skipping");
                return null;
            }
        }

        try
        {
            return await ReconcileAsync(trigger, ct);
        }
        finally
        {
            if (relational)
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", LockKey);
                await _db.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<ReconciliationRun> ReconcileAsync(string trigger, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _options.LookbackDays));
        var run = new ReconciliationRun(trigger, since);
        _db.ReconciliationRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var cancelled = _db.CancelledOrders.Select(c => c.OrderId);
        // everything recent, plus older charges for cancelled orders that are still not refunded
        var payments = await _db.Payments
            .Where(p => p.CreatedAt >= since || (p.Status == PaymentStatus.Succeeded && cancelled.Contains(p.OrderId)))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
        var cancellations = await _db.CancelledOrders.Where(c => payments.Select(p => p.OrderId).Contains(c.OrderId))
            .ToDictionaryAsync(c => c.OrderId, ct);

        foreach (var payment in payments)
        {
            await CheckAsync(run, payment, cancellations.GetValueOrDefault(payment.OrderId), ct);
            await _db.SaveChangesAsync(ct);
        }

        run.Finish();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Reconciliation {RunId}: checked {Checked}, unverified {Unverified}, issues {Issues}, refunded {Refunded}",
            run.Id, run.Checked, run.Unverified, run.Issues.Count, run.Refunded);
        return run;
    }

    private async Task CheckAsync(ReconciliationRun run, Payment payment, CancelledOrder? cancellation, CancellationToken ct)
    {
        ProviderPayment? remote = null;
        if (payment.ProviderReference is not null && payment.Provider == _provider.Name)
        {
            remote = await _provider.LookupAsync(payment.ProviderReference, ct);
        }

        run.Count(verified: remote is not null);
        var chargedRemotely = remote?.Status == ProviderPaymentStatus.Succeeded && remote.AmountRefunded < remote.Amount;

        switch (payment.Status)
        {
            case PaymentStatus.Succeeded when cancellation is not null && (remote is null || chargedRemotely):
                await RefundAsync(run, payment, ReconciliationIssueKind.PaidButCancelled,
                    $"Order was cancelled ({cancellation.Reason}) after the payment went through.", ct);
                return;

            case PaymentStatus.Failed when chargedRemotely:
                await RefundAsync(run, payment, ReconciliationIssueKind.ChargedButMarkedFailed,
                    $"Recorded as failed ({payment.FailureReason}) but {_provider.Name} shows {remote!.Amount:0.00} {remote.Currency} taken.", ct);
                return;
        }

        if (remote is null || payment.Status != PaymentStatus.Succeeded)
        {
            return;
        }

        if (remote.Status == ProviderPaymentStatus.NotFound)
        {
            run.Flag(payment, ReconciliationIssueKind.MissingAtProvider, $"{_provider.Name} has no payment {payment.ProviderReference}.",
                ReconciliationResolution.NeedsReview);
        }
        else if (remote.Status != ProviderPaymentStatus.Succeeded)
        {
            run.Flag(payment, ReconciliationIssueKind.StatusMismatch, $"Recorded as succeeded, {_provider.Name} says {remote.Status}.",
                ReconciliationResolution.NeedsReview);
        }
        else if (remote.Amount != payment.Amount)
        {
            run.Flag(payment, ReconciliationIssueKind.AmountMismatch,
                $"Recorded {payment.Amount:0.00} {payment.Currency}, {_provider.Name} charged {remote.Amount:0.00} {remote.Currency}.",
                ReconciliationResolution.NeedsReview);
        }
    }

    private async Task RefundAsync(ReconciliationRun run, Payment payment, ReconciliationIssueKind kind, string detail, CancellationToken ct)
    {
        RefundResult result;
        try
        {
            result = await _provider.RefundAsync(new RefundRequest(payment.OrderId, payment.ProviderReference ?? string.Empty, payment.Amount,
                detail, RefundKeyFor(payment.OrderId)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new RefundResult(false, null, ex.Message);
        }

        if (!result.Succeeded)
        {
            // the next run tries again with the same idempotency key
            _logger.LogWarning("Refund for order {OrderId} failed: {Reason}", payment.OrderId, result.FailureReason);
            run.Flag(payment, kind, $"{detail} Refund failed: {result.FailureReason}", ReconciliationResolution.RefundFailed);
            return;
        }

        payment.MarkRefunded(result.RefundReference ?? string.Empty, detail);
        run.Flag(payment, kind, detail, ReconciliationResolution.Refunded);
        _db.Enqueue(Topics.PaymentRefunded, payment.OrderId, EventTypes.PaymentRefunded,
            new PaymentRefunded(payment.OrderId, payment.Id, payment.UserId, payment.Email, payment.Amount, payment.Currency, detail,
                payment.RefundReference));
        _logger.LogInformation("Refunded {Amount} {Currency} for order {OrderId} ({Kind})", payment.Amount, payment.Currency, payment.OrderId, kind);
    }
}
