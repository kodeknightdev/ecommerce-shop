using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceShop.Catalog.Data.Migrations;

/// <inheritdoc />
public partial class ProductReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "RatingAverage",
            table: "products",
            type: "numeric(3,2)",
            precision: 3,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<int>(
            name: "ReviewCount",
            table: "products",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "processed_events",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_processed_events", x => x.EventId);
            });

        migrationBuilder.CreateTable(
            name: "product_reviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Rating = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                HiddenReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_reviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_product_reviews_products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "verified_purchases",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_verified_purchases", x => new { x.UserId, x.ProductId });
            });

        migrationBuilder.CreateIndex(
            name: "IX_product_reviews_ProductId_IsHidden_CreatedAt",
            table: "product_reviews",
            columns: new[] { "ProductId", "IsHidden", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_product_reviews_ProductId_UserId",
            table: "product_reviews",
            columns: new[] { "ProductId", "UserId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "processed_events");

        migrationBuilder.DropTable(
            name: "product_reviews");

        migrationBuilder.DropTable(
            name: "verified_purchases");

        migrationBuilder.DropColumn(
            name: "RatingAverage",
            table: "products");

        migrationBuilder.DropColumn(
            name: "ReviewCount",
            table: "products");
    }
}
