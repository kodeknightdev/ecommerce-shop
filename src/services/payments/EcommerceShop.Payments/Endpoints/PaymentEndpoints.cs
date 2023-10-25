using System.Security.Claims;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Errors;
using EcommerceShop.Payments.Application;
using EcommerceShop.Payments.Data;
using EcommerceShop.Payments.Domain;
using EcommerceShop.Payments.Providers;
using Microsoft.EntityFrameworkCore;

namespace EcommerceShop.Payments.Endpoints;

public sealed record PaymentDto(Guid Id, Guid OrderId, decimal Amount, string Currency, string Provider, string Status, string? FailureReason,
    string? ProviderReference, DateTimeOffset CreatedAt, string? RefundReference, DateTimeOffset? RefundedAt);

public sealed record ReconciliationIssueDto(Guid Id, Guid PaymentId, Guid OrderId, string Kind, string Resolution, string Detail, DateTimeOffset CreatedAt);

public sealed record ReconciliationRunDto(Guid Id, string Trigger, DateTimeOffset Since, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    int Checked, int Unverified, int IssueCount, int Refunded, IReadOnlyList<ReconciliationIssueDto>? Issues);

public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments").RequireAuthorization(AuthPolicies.Authenticated);

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, ClaimsPrincipal user, PaymentsDbContext db, CancellationToken ct) =>
        {
            var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == orderId, ct);
            if (payment is null || (payment.UserId != user.GetUserId() && !user.IsAdmin()))
            {
                throw new NotFoundException($"No payment for order {orderId}.");
            }

            return Results.Ok(ToDto(payment));
        }).Produces<PaymentDto>();

        group.MapGet("", async (PaymentsDbContext db, string? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var query = db.Payments.AsNoTracking();
            if (Enum.TryParse<PaymentStatus>(status, true, out var parsed))
            {
                query = query.Where(p => p.Status == parsed);
            }

            var p = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
            return Results.Ok(new { items = items.Select(ToDto), page = p, pageSize = size, total });
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/provider", (IPaymentProvider provider) => Results.Ok(new { provider = provider.Name }))
            .AllowAnonymous();

        var reconciliation = group.MapGroup("/reconciliation").RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Reconciliation");

        reconciliation.MapPost("/runs", async (ReconciliationService service, CancellationToken ct) =>
        {
            var run = await service.RunAsync("manual", ct) ?? throw new ConflictException("A reconciliation run is already in progress.");
            return Results.Ok(ToDto(run, includeIssues: true));
        }).Produces<ReconciliationRunDto>().ProducesProblem(409);

        reconciliation.MapGet("/runs", async (PaymentsDbContext db, int? page, CancellationToken ct) =>
        {
            var p = Math.Max(1, page ?? 1);
            var runs = await db.ReconciliationRuns.AsNoTracking().Include(r => r.Issues)
                .OrderByDescending(r => r.StartedAt).Skip((p - 1) * 20).Take(20).ToListAsync(ct);
            return Results.Ok(runs.Select(r => ToDto(r, includeIssues: false)));
        }).Produces<List<ReconciliationRunDto>>();

        reconciliation.MapGet("/runs/{id:guid}", async (Guid id, PaymentsDbContext db, CancellationToken ct) =>
        {
            var run = await db.ReconciliationRuns.AsNoTracking().Include(r => r.Issues).SingleOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException($"Reconciliation run {id} not found.");
            return Results.Ok(ToDto(run, includeIssues: true));
        }).Produces<ReconciliationRunDto>().ProducesProblem(404);

        return group;
    }

    private static PaymentDto ToDto(Payment p) =>
        new(p.Id, p.OrderId, p.Amount, p.Currency, p.Provider, p.Status.ToString(), p.FailureReason, p.ProviderReference, p.CreatedAt,
            p.RefundReference, p.RefundedAt);

    private static ReconciliationRunDto ToDto(ReconciliationRun r, bool includeIssues) =>
        new(r.Id, r.Trigger, r.Since, r.StartedAt, r.FinishedAt, r.Checked, r.Unverified, r.Issues.Count, r.Refunded,
            includeIssues
                ? r.Issues.OrderBy(i => i.CreatedAt).Select(i => new ReconciliationIssueDto(i.Id, i.PaymentId, i.OrderId, i.Kind.ToString(),
                    i.Resolution.ToString(), i.Detail, i.CreatedAt)).ToList()
                : null);
}
