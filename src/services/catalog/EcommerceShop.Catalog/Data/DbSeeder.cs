using EcommerceShop.Catalog.Domain;
using EcommerceShop.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EcommerceShop.Catalog.Data;

public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, ILogger logger, bool seedDemoData)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (!seedDemoData || await db.Categories.AnyAsync())
        {
            return;
        }

        db.Categories.AddRange(SeedData.Categories.Select(c => new Category(c.Id, c.Name, c.Slug, $"Hand-picked {c.Name.ToLowerInvariant()}.")));
        db.Products.AddRange(SeedData.Products.Select(p => new Product(
            p.Id, p.CategoryId, p.Sku, p.Name, p.Slug,
            $"{p.Name} - an ecommerce-shop favourite. Built to last and priced fairly.",
            p.Price,
            $"https://picsum.photos/seed/{p.Slug}/640/480",
            p.Attributes)));

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Categories} categories and {Products} products", SeedData.Categories.Length, SeedData.Products.Length);

        await SeedDemoReviewsAsync(db);
    }

    // Jane's two confirmed demo orders (see the orders seeder) make her a verified buyer of four products;
    // she has reviewed three, so the fourth shows the "write a review" form
    private static async Task SeedDemoReviewsAsync(CatalogDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var purchases = new (string Slug, Guid OrderId, int DaysAgo)[]
        {
            ("aurora-wireless-headphones", DemoAccounts.JaneOrder1, 21),
            ("night-garden", DemoAccounts.JaneOrder1, 21),
            ("flow-yoga-mat", DemoAccounts.JaneOrder2, 9),
            ("ridge-insulated-bottle", DemoAccounts.JaneOrder2, 9)
        };
        var reviews = new (string Slug, int Rating, string? Title, string Comment)[]
        {
            ("aurora-wireless-headphones", 5, "Great on long flights", "Noise cancelling is better than I expected and the battery lasts all week."),
            ("night-garden", 4, "Read it in two evenings", "Lovely writing. The middle drags a little, but the ending makes up for it."),
            ("flow-yoga-mat", 4, null, "Good grip and thick enough for my knees. Smelled a bit for the first few days.")
        };

        var products = await db.Products.Where(p => purchases.Select(x => x.Slug).Contains(p.Slug)).ToDictionaryAsync(p => p.Slug);
        foreach (var (slug, orderId, daysAgo) in purchases)
        {
            db.VerifiedPurchases.Add(new VerifiedPurchase(DemoAccounts.JaneId, products[slug].Id, orderId, now.AddDays(-daysAgo)));
        }

        foreach (var (slug, rating, title, comment) in reviews)
        {
            db.Reviews.Add(new ProductReview(products[slug].Id, DemoAccounts.JaneId, "Jane D.", rating, title, comment));
            products[slug].SetRating(1, rating);
        }

        await db.SaveChangesAsync();
    }
}
