namespace EcommerceShop.Catalog.Domain;

// one per user and product (unique index); only buyers get to write one
public sealed class ProductReview
{
    private ProductReview() { }

    public ProductReview(Guid productId, Guid userId, string authorName, int rating, string? title, string comment)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        Edit(authorName, rating, title, comment);
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public string? Title { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public bool IsHidden { get; private set; }
    public string? HiddenReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Edit(string authorName, int rating, string? title, string comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        AuthorName = authorName;
        Rating = rating;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Comment = comment.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetHidden(bool hidden, string? reason)
    {
        IsHidden = hidden;
        HiddenReason = hidden ? reason : null;
    }
}

// filled from orders.order-confirmed; catalog never asks orders who bought what
public sealed class VerifiedPurchase
{
    private VerifiedPurchase() { }

    public VerifiedPurchase(Guid userId, Guid productId, Guid orderId, DateTimeOffset purchasedAt)
    {
        UserId = userId;
        ProductId = productId;
        OrderId = orderId;
        PurchasedAt = purchasedAt;
    }

    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateTimeOffset PurchasedAt { get; private set; }
}
