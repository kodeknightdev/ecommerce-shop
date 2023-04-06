using FluentValidation;

namespace EcommerceShop.Catalog.Endpoints;

public sealed record ReviewDto(Guid Id, Guid ProductId, string AuthorName, int Rating, string? Title, string Comment,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ReviewPage(IReadOnlyList<ReviewDto> Items, int Page, int PageSize, int Total, decimal AverageRating, int ReviewCount,
    IReadOnlyDictionary<int, int> Distribution);

// what the product page needs to decide between "write", "edit" and nothing
public sealed record MyReviewDto(bool CanReview, ReviewDto? Review, bool Hidden);

public sealed record AdminReviewDto(Guid Id, Guid ProductId, string ProductName, Guid UserId, string AuthorName, int Rating, string? Title,
    string Comment, bool Hidden, string? HiddenReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record UpsertReviewRequest(int Rating, string? Title, string Comment);

public sealed record ReviewVisibilityRequest(bool Hidden, string? Reason);

public sealed class UpsertReviewRequestValidator : AbstractValidator<UpsertReviewRequest>
{
    public UpsertReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(120);
        RuleFor(x => x.Comment).NotEmpty().MinimumLength(3).MaximumLength(2000);
    }
}

public sealed class ReviewVisibilityRequestValidator : AbstractValidator<ReviewVisibilityRequest>
{
    public ReviewVisibilityRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}
