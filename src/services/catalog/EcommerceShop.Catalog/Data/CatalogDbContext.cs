using System.Text.Json;
using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EcommerceShop.Catalog.Data;

public sealed class CatalogDbContext : DbContext, IInboxDbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductReview> Reviews => Set<ProductReview>();
    public DbSet<VerifiedPurchase> VerifiedPurchases => Set<VerifiedPurchase>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("categories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(c => c.Slug).IsUnique();
            b.Property(c => c.Description).HasMaxLength(500);
        });

        var attributesComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => new Dictionary<string, string>(d));

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Sku).HasMaxLength(32).IsRequired();
            b.HasIndex(p => p.Sku).IsUnique();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Slug).HasMaxLength(200).IsRequired();
            b.HasIndex(p => p.Slug).IsUnique();
            b.Property(p => p.Description).HasMaxLength(4000);
            b.Property(p => p.Price).HasPrecision(12, 2);
            b.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            b.Property(p => p.ImageUrl).HasMaxLength(500);
            b.Property(p => p.Attributes)
                .HasColumnType("jsonb")
                .HasConversion(
                    d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(attributesComparer);
            b.Property(p => p.RatingAverage).HasPrecision(3, 2);
            b.Ignore(p => p.PriceCents);
            b.HasIndex(p => new { p.CategoryId, p.Price });
            b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductReview>(b =>
        {
            b.ToTable("product_reviews");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
            b.HasIndex(r => new { r.ProductId, r.IsHidden, r.CreatedAt });
            b.Property(r => r.AuthorName).HasMaxLength(100).IsRequired();
            b.Property(r => r.Title).HasMaxLength(120);
            b.Property(r => r.Comment).HasMaxLength(2000).IsRequired();
            b.Property(r => r.HiddenReason).HasMaxLength(200);
            b.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VerifiedPurchase>(b =>
        {
            b.ToTable("verified_purchases");
            b.HasKey(v => new { v.UserId, v.ProductId });
        });

        modelBuilder.ConfigureInbox();
    }
}
