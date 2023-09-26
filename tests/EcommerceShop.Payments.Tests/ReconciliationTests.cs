using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using EcommerceShop.Contracts;
using EcommerceShop.Payments.Application;
using EcommerceShop.Payments.Data;
using EcommerceShop.Payments.Domain;
using EcommerceShop.Payments.Providers;

namespace EcommerceShop.Payments.Tests;

// a provider whose records the test can bend
public sealed class LedgerProvider : IPaymentProvider
{
    public Dictionary<string, ProviderPayment> Ledger { get; } = new();
    public List<RefundRequest> Refunds { get; } = new();
    public bool FailRefunds { get; set; }
    public string Name => "mock";

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        var reference = $"mock_pi_{request.OrderId:N}";
        Ledger[reference] = new ProviderPayment(reference, ProviderPaymentStatus.Succeeded, request.Amount, request.Currency, 0);
        return Task.FromResult(ChargeResult.Success(reference));
    }

    public Task<ProviderPayment?> LookupAsync(string providerReference, CancellationToken ct) =>
        Task.FromResult(Ledger.TryGetValue(providerReference, out var p) ? p : null);

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        if (FailRefunds)
        {
            throw new HttpRequestException("provider unavailable");
        }

        Refunds.Add(request);
        return Task.FromResult(new RefundResult(true, $"mock_re_{request.OrderId:N}", null));
    }
}

public class ReconciliationTests
{
    private readonly PaymentsDbContext _db = new(new DbContextOptionsBuilder<PaymentsDbContext>().UseInMemoryDatabase($"rec-{Guid.NewGuid():N}").Options);
    private readonly LedgerProvider _provider = new();

    private ReconciliationService Service => new(_db, _provider, Options.Create(new ReconciliationOptions()), NullLogger<ReconciliationService>.Instance);
    private PaymentProcessor Processor => new(_db, _provider, NullLogger<PaymentProcessor>.Instance);

    private async Task<Payment> Charge(decimal total = 42.5m)
    {
        var e = new StockReserved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "casey@example.com", total, "USD", "pm_card_visa",
            DateTimeOffset.UtcNow.AddMinutes(15));
        return (await Processor.HandleStockReservedAsync(Guid.NewGuid(), e, default))!;
    }

    private Task Cancel(Guid orderId) =>
        Processor.HandleOrderCancelledAsync(Guid.NewGuid(), new OrderCancelled(orderId, Guid.NewGuid(), "casey@example.com", "Inventory: Reservation expired"),
            DateTimeOffset.UtcNow, default);

    private List<string> OutboxTopics() => _db.OutboxMessages.AsNoTracking().OrderBy(m => m.CreatedAt).Select(m => m.Topic).ToList();

    [Fact]
    public async Task Matching_records_produce_no_issues()
    {
        await Charge();
        var run = (await Service.RunAsync("manual", default))!;

        run.Checked.Should().Be(1);
        run.Unverified.Should().Be(0);
        run.Issues.Should().BeEmpty();
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Paid_but_cancelled_order_is_refunded_once_and_announced()
    {
        var payment = await Charge();
        await Cancel(payment.OrderId);

        var run = (await Service.RunAsync("schedule", default))!;

        var issue = run.Issues.Should().ContainSingle().Subject;
        issue.Kind.Should().Be(ReconciliationIssueKind.PaidButCancelled);
        issue.Resolution.Should().Be(ReconciliationResolution.Refunded);
        _provider.Refunds.Should().ContainSingle().Which.IdempotencyKey.Should().Be($"refund-{payment.OrderId}");
        (await _db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Refunded);
        OutboxTopics().Should().EndWith(Topics.PaymentRefunded);

        var refunded = JsonDocument.Parse(_db.OutboxMessages.AsNoTracking().Single(m => m.Topic == Topics.PaymentRefunded).Payload).RootElement;
        refunded.GetProperty("payload").GetProperty("amount").GetDecimal().Should().Be(42.5m);

        var again = (await Service.RunAsync("schedule", default))!;
        again.Issues.Should().BeEmpty();
        _provider.Refunds.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_failed_refund_is_reported_and_tried_again_next_run()
    {
        var payment = await Charge();
        await Cancel(payment.OrderId);
        _provider.FailRefunds = true;

        var first = (await Service.RunAsync("schedule", default))!;
        first.Issues.Should().ContainSingle().Which.Resolution.Should().Be(ReconciliationResolution.RefundFailed);
        (await _db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Succeeded);

        _provider.FailRefunds = false;
        var second = (await Service.RunAsync("schedule", default))!;
        second.Refunded.Should().Be(1);
    }

    [Fact]
    public async Task Money_taken_for_a_payment_we_marked_failed_is_refunded()
    {
        var e = new StockReserved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "casey@example.com", 30m, "USD", "pm_card_chargeDeclined",
            DateTimeOffset.UtcNow.AddMinutes(15));
        var declined = await new PaymentProcessor(_db, new MockPaymentProvider(TimeSpan.Zero), NullLogger<PaymentProcessor>.Instance)
            .HandleStockReservedAsync(Guid.NewGuid(), e, default);
        _provider.Ledger[declined!.ProviderReference!] = new ProviderPayment(declined.ProviderReference!, ProviderPaymentStatus.Succeeded, 30m, "USD", 0);

        var run = (await Service.RunAsync("manual", default))!;

        run.Issues.Should().ContainSingle(i => i.Kind == ReconciliationIssueKind.ChargedButMarkedFailed && i.Resolution == ReconciliationResolution.Refunded);
    }

    [Fact]
    public async Task Amount_and_status_mismatches_are_flagged_for_review_not_fixed()
    {
        var a = await Charge(10m);
        var b = await Charge(20m);
        var c = await Charge(30m);
        _provider.Ledger[a.ProviderReference!] = _provider.Ledger[a.ProviderReference!] with { Amount = 12m };
        _provider.Ledger[b.ProviderReference!] = _provider.Ledger[b.ProviderReference!] with { Status = ProviderPaymentStatus.Failed };
        _provider.Ledger[c.ProviderReference!] = _provider.Ledger[c.ProviderReference!] with { Status = ProviderPaymentStatus.NotFound };

        var run = (await Service.RunAsync("manual", default))!;

        run.Issues.Select(i => i.Kind).Should().BeEquivalentTo(new[]
        {
            ReconciliationIssueKind.AmountMismatch, ReconciliationIssueKind.StatusMismatch, ReconciliationIssueKind.MissingAtProvider
        });
        run.Issues.Should().OnlyContain(i => i.Resolution == ReconciliationResolution.NeedsReview);
        _provider.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task Payments_the_provider_cannot_see_are_counted_as_unverified()
    {
        var payment = await Charge();
        _provider.Ledger.Clear();

        var run = (await Service.RunAsync("manual", default))!;

        run.Unverified.Should().Be(1);
        run.Issues.Should().BeEmpty();
        (await _db.Payments.SingleAsync(p => p.Id == payment.Id)).Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Does_not_charge_an_order_it_already_knows_is_cancelled()
    {
        var orderId = Guid.NewGuid();
        await Cancel(orderId);
        var e = new StockReserved(orderId, Guid.NewGuid(), Guid.NewGuid(), "casey@example.com", 10m, "USD", "pm_card_visa", DateTimeOffset.UtcNow);

        (await Processor.HandleStockReservedAsync(Guid.NewGuid(), e, default)).Should().BeNull();
        _provider.Ledger.Should().BeEmpty();
        OutboxTopics().Should().BeEmpty();
    }
}
