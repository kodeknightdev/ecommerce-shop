using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Errors;
using EcommerceShop.BuildingBlocks.Validation;
using EcommerceShop.Catalog.Caching;
using EcommerceShop.Catalog.Data;
using EcommerceShop.Catalog.Domain;

namespace EcommerceShop.Catalog.Endpoints;

public static class ReviewEndpoints
{
    public const int MaxPageSize = 50;

    public static RouteGroupBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Reviews");

        group.MapGet("/{id:guid}/reviews", List).Produces<ReviewPage>().ProducesProblem(404);

        var mine = group.MapGroup("/{id:guid}/reviews/me").RequireAuthorization(AuthPolicies.Authenticated);
        mine.MapGet("/", GetMine).Produces<MyReviewDto>();
        mine.MapPut("/", Upsert).Validate<UpsertReviewRequest>().Produces<ReviewDto>().Produces<ReviewDto>(201).ProducesProblem(403);
        mine.MapDelete("/", DeleteMine).Produces(204);

        var admin = group.MapGroup("/reviews").RequireAuthorization(AuthPolicies.AdminOnly);
        admin.MapGet("/", AdminList).Produces<PagedResult<AdminReviewDto>>();
        admin.MapPut("/{reviewId:guid}/visibility", SetVisibility).Validate<ReviewVisibilityRequest>().Produces<AdminReviewDto>().ProducesProblem(404);

        return group;
    }

    private static async Task<IResult> List(Guid id, CatalogDbContext db, string? sort, int? page, int? pageSize, CancellationToken ct)
    {
        await EnsureProduct(db, id, ct);
        var visible = db.Reviews.AsNoTracking().Where(r => r.ProductId == id && !r.IsHidden);

        var counts = await visible.GroupBy(r => r.Rating).Select(g => new { Rating = g.Key, Count = g.Count() }).ToListAsync(ct);
        var distribution = Enumerable.Range(1, 5).ToDictionary(n => n, n => counts.FirstOrDefault(c => c.Rating == n)?.Count ?? 0);
        var total = counts.Sum(c => c.Count);
        var average = total == 0 ? 0 : Math.Round((decimal)counts.Sum(c => c.Rating * c.Count) / total, 2);

        var p = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 10, 1, MaxPageSize);
        var ordered = sort?.ToLowerInvariant() switch
        {
            "highest" => visible.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "lowest" => visible.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => visible.OrderByDescending(r => r.CreatedAt)
        };
        var items = await ordered.Skip((p - 1) * size).Take(size).ToListAsync(ct);

        return Results.Ok(new ReviewPage(items.Select(ToDto).ToList(), p, size, total, average, total, distribution));
    }

    private static async Task<IResult> GetMine(Guid id, ClaimsPrincipal user, CatalogDbContext db, CancellationToken ct)
    {
        await EnsureProduct(db, id, ct);
        var userId = user.GetUserId();
        var review = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(r => r.ProductId == id && r.UserId == userId, ct);
        var bought = await db.VerifiedPurchases.AnyAsync(v => v.ProductId == id && v.UserId == userId, ct);
        return Results.Ok(new MyReviewDto(bought, review is null ? null : ToDto(review), review?.IsHidden ?? false));
    }

    private static async Task<IResult> Upsert(Guid id, UpsertReviewRequest request, ClaimsPrincipal user, CatalogDbContext db, ProductCache cache,
        CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException($"Product {id} not found.");
        var userId = user.GetUserId();
        if (!await db.VerifiedPurchases.AnyAsync(v => v.ProductId == id && v.UserId == userId, ct))
        {
            throw new ForbiddenException("Only customers who bought this product can review it.");
        }

        var review = await db.Reviews.SingleOrDefaultAsync(r => r.ProductId == id && r.UserId == userId, ct);
        var created = review is null;
        if (review is null)
        {
            review = new ProductReview(id, userId, AuthorName(user), request.Rating, request.Title, request.Comment);
            db.Reviews.Add(review);
        }
        else
        {
            review.Edit(AuthorName(user), request.Rating, request.Title, request.Comment);
        }

        await db.SaveChangesAsync(ct);
        await RefreshRatingAsync(db, product, cache, ct);
        return created ? Results.Created($"/api/products/{id}/reviews/me", ToDto(review)) : Results.Ok(ToDto(review));
    }

    private static async Task<IResult> DeleteMine(Guid id, ClaimsPrincipal user, CatalogDbContext db, ProductCache cache, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException($"Product {id} not found.");
        var userId = user.GetUserId();
        var review = await db.Reviews.SingleOrDefaultAsync(r => r.ProductId == id && r.UserId == userId, ct);
        if (review is not null)
        {
            db.Reviews.Remove(review);
            await db.SaveChangesAsync(ct);
            await RefreshRatingAsync(db, product, cache, ct);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> AdminList(CatalogDbContext db, bool? hidden, int? rating, int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking();
        if (hidden is not null)
        {
            query = query.Where(r => r.IsHidden == hidden);
        }

        if (rating is not null)
        {
            query = query.Where(r => r.Rating == rating);
        }

        var p = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 20, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).Skip((p - 1) * size).Take(size)
            .Join(db.Products, r => r.ProductId, pr => pr.Id, (r, pr) => new { Review = r, ProductName = pr.Name })
            .ToListAsync(ct);

        var items = rows.Select(x => ToAdminDto(x.Review, x.ProductName)).ToList();
        return Results.Ok(new PagedResult<AdminReviewDto>(items, p, size, total));
    }

    private static async Task<IResult> SetVisibility(Guid reviewId, ReviewVisibilityRequest request, CatalogDbContext db, ProductCache cache,
        CancellationToken ct)
    {
        var review = await db.Reviews.SingleOrDefaultAsync(r => r.Id == reviewId, ct) ?? throw new NotFoundException($"Review {reviewId} not found.");
        var product = await db.Products.SingleAsync(p => p.Id == review.ProductId, ct);
        review.SetHidden(request.Hidden, request.Reason);
        await db.SaveChangesAsync(ct);
        await RefreshRatingAsync(db, product, cache, ct);
        return Results.Ok(ToAdminDto(review, product.Name));
    }

    // recomputed from the table rather than adjusted in place, so a missed update heals on the next write
    internal static async Task RefreshRatingAsync(CatalogDbContext db, Product product, ProductCache? cache, CancellationToken ct)
    {
        var visible = db.Reviews.Where(r => r.ProductId == product.Id && !r.IsHidden);
        var count = await visible.CountAsync(ct);
        var average = count == 0 ? 0 : await visible.AverageAsync(r => (decimal)r.Rating, ct);
        product.SetRating(count, average);
        await db.SaveChangesAsync(ct);
        if (cache is not null)
        {
            await cache.InvalidateAsync(product.Id, product.Slug);
        }
    }

    // "Jane Doe" -> "Jane D."
    internal static string AuthorName(ClaimsPrincipal user)
    {
        var name = user.FindFirstValue("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            var email = user.GetEmail();
            return email.Contains('@') ? email[..email.IndexOf('@')] : "Customer";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0] : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }

    private static async Task EnsureProduct(CatalogDbContext db, Guid id, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == id, ct))
        {
            throw new NotFoundException($"Product {id} not found.");
        }
    }

    private static ReviewDto ToDto(ProductReview r) => new(r.Id, r.ProductId, r.AuthorName, r.Rating, r.Title, r.Comment, r.CreatedAt, r.UpdatedAt);

    private static AdminReviewDto ToAdminDto(ProductReview r, string productName) =>
        new(r.Id, r.ProductId, productName, r.UserId, r.AuthorName, r.Rating, r.Title, r.Comment, r.IsHidden, r.HiddenReason, r.CreatedAt, r.UpdatedAt);
}
