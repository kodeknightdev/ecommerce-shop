using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using EcommerceShop.Catalog.Data;
using EcommerceShop.Catalog.Endpoints;
using EcommerceShop.Catalog.Messaging;
using EcommerceShop.Contracts;
using EcommerceShop.TestSupport;

namespace EcommerceShop.Catalog.IntegrationTests;

public class ReviewApiTests : IClassFixture<CatalogApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid Headphones = Guid.Parse("84db4db8-b982-50fd-9f18-1156b1cfeb6d");
    private static readonly Guid Bottle = Guid.Parse("b09a7e72-39b3-559c-8e31-f7549f90dbf5");
    private static readonly Guid Speaker = Guid.Parse("738cd2fe-3f44-5eac-bfdc-a1e5ca0d14ab");
    private readonly CatalogApiFactory _factory;

    public ReviewApiTests(CatalogApiFactory factory) => _factory = factory;

    private HttpClient As(Guid userId, string role = Roles.Customer) =>
        _factory.CreateClient().WithToken(TestTokens.Create(userId, $"{userId:N}@example.com", role));

    private Task<int> Confirm(Guid userId, Guid eventId, params Guid[] productIds) =>
        _factory.WithDbAsync(db => PurchaseRecorder.RecordAsync(db, eventId,
            new OrderConfirmed(Guid.NewGuid(), userId, "x@example.com", 10m, "USD",
                productIds.Select(p => new OrderLineDto(p, "SKU", "Name", 1, 10m)).ToList()),
            DateTimeOffset.UtcNow, NullLogger.Instance, default));

    private static object Review(int rating, string comment = "Does what it says.") => new { rating, title = "Solid", comment };

    [Fact]
    public async Task Seeded_reviews_show_with_rating_on_the_product()
    {
        var page = await _factory.CreateClient().GetFromJsonAsync<ReviewPage>($"/api/products/{Headphones}/reviews", Json);
        page!.Items.Should().ContainSingle(r => r.AuthorName == "Jane D." && r.Rating == 5);

        var product = await _factory.CreateClient().GetFromJsonAsync<ProductDetailDto>($"/api/products/{Headphones}", Json);
        product!.ReviewCount.Should().Be(1);
        product.Rating.Should().Be(5);
    }

    [Fact]
    public async Task Customers_who_did_not_buy_cannot_review()
    {
        var client = As(Guid.NewGuid());
        var mine = await client.GetFromJsonAsync<MyReviewDto>($"/api/products/{Speaker}/reviews/me", Json);
        mine!.CanReview.Should().BeFalse();

        var response = await client.PutAsJsonAsync($"/api/products/{Speaker}/reviews/me", Review(4));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("bought");
    }

    [Fact]
    public async Task A_confirmed_order_unlocks_one_review_per_product_that_can_be_edited()
    {
        var userId = Guid.NewGuid();
        (await Confirm(userId, Guid.NewGuid(), Speaker)).Should().Be(1);
        var client = As(userId);

        var created = await client.PutAsJsonAsync($"/api/products/{Speaker}/reviews/me", Review(2));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var updated = await client.PutAsJsonAsync($"/api/products/{Speaker}/reviews/me", Review(4, "Better after a firmware update."));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await client.GetFromJsonAsync<ReviewPage>($"/api/products/{Speaker}/reviews", Json);
        page!.Items.Should().ContainSingle().Which.Comment.Should().Be("Better after a firmware update.");
        page.Distribution[4].Should().Be(1);

        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{Speaker}", Json);
        product!.Rating.Should().Be(4);

        (await client.DeleteAsync($"/api/products/{Speaker}/reviews/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{Speaker}", Json))!.ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task The_same_order_confirmed_event_twice_is_recorded_once()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        (await Confirm(userId, eventId, Bottle)).Should().Be(1);
        (await Confirm(userId, eventId, Bottle)).Should().Be(0);
        (await _factory.WithDbAsync(db => db.VerifiedPurchases.CountAsync(v => v.UserId == userId))).Should().Be(1);
    }

    [Fact]
    public async Task Rejects_ratings_outside_one_to_five()
    {
        var userId = Guid.NewGuid();
        await Confirm(userId, Guid.NewGuid(), Bottle);
        var response = await As(userId).PutAsJsonAsync($"/api/products/{Bottle}/reviews/me", Review(6));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_hide_a_review_which_drops_it_from_the_list_and_rating()
    {
        var userId = Guid.NewGuid();
        var product = Guid.Parse("a74b6016-9f5b-5624-8e3d-70d3c8df3884");
        await Confirm(userId, Guid.NewGuid(), product);
        var review = await (await As(userId).PutAsJsonAsync($"/api/products/{product}/reviews/me", Review(1, "Buy it, it is spam."))).Content
            .ReadFromJsonAsync<ReviewDto>(Json);

        var admin = As(Guid.NewGuid(), Roles.Admin);
        (await As(userId).PutAsJsonAsync($"/api/products/reviews/{review!.Id}/visibility", new { hidden = true })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        var hidden = await admin.PutAsJsonAsync($"/api/products/reviews/{review.Id}/visibility", new { hidden = true, reason = "Spam" });
        hidden.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _factory.CreateClient().GetFromJsonAsync<ReviewPage>($"/api/products/{product}/reviews", Json);
        page!.Total.Should().Be(0);
        (await _factory.CreateClient().GetFromJsonAsync<ProductDetailDto>($"/api/products/{product}", Json))!.ReviewCount.Should().Be(0);

        var moderation = await admin.GetFromJsonAsync<PagedResult<AdminReviewDto>>("/api/products/reviews?hidden=true", Json);
        moderation!.Items.Should().Contain(r => r.Id == review.Id && r.HiddenReason == "Spam");

        var mine = await As(userId).GetFromJsonAsync<MyReviewDto>($"/api/products/{product}/reviews/me", Json);
        mine!.Hidden.Should().BeTrue();
    }
}
