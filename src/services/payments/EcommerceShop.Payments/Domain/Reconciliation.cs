namespace EcommerceShop.Payments.Domain;

public enum ReconciliationIssueKind
{
    // charged, but the order was cancelled
    PaidButCancelled,
    // we say failed, the provider says the money was taken
    ChargedButMarkedFailed,
    // we say succeeded, the provider disagrees
    StatusMismatch,
    AmountMismatch,
    MissingAtProvider
}

public enum ReconciliationResolution
{
    Refunded,
    RefundFailed,
    NeedsReview
}

public sealed class ReconciliationRun
{
    private readonly List<ReconciliationIssue> _issues = new();

    private ReconciliationRun() { }

    public ReconciliationRun(string trigger, DateTimeOffset since)
    {
        Id = Guid.NewGuid();
        Trigger = trigger;
        Since = since;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Trigger { get; private set; } = string.Empty;
    public DateTimeOffset Since { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int Checked { get; private set; }
    // the provider couldn't tell us anything (e.g. the mock after a restart)
    public int Unverified { get; private set; }
    public int Refunded { get; private set; }
    public IReadOnlyList<ReconciliationIssue> Issues => _issues;

    public void Count(bool verified)
    {
        Checked++;
        if (!verified)
        {
            Unverified++;
        }
    }

    public ReconciliationIssue Flag(Payment payment, ReconciliationIssueKind kind, string detail, ReconciliationResolution resolution)
    {
        var issue = new ReconciliationIssue(Id, payment, kind, detail, resolution);
        _issues.Add(issue);
        if (resolution == ReconciliationResolution.Refunded)
        {
            Refunded++;
        }

        return issue;
    }

    public void Finish() => FinishedAt = DateTimeOffset.UtcNow;
}

public sealed class ReconciliationIssue
{
    private ReconciliationIssue() { }

    public ReconciliationIssue(Guid runId, Payment payment, ReconciliationIssueKind kind, string detail, ReconciliationResolution resolution)
    {
        Id = Guid.NewGuid();
        RunId = runId;
        PaymentId = payment.Id;
        OrderId = payment.OrderId;
        Kind = kind;
        Detail = detail.Length > 500 ? detail[..500] : detail;
        Resolution = resolution;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    public ReconciliationIssueKind Kind { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public ReconciliationResolution Resolution { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
