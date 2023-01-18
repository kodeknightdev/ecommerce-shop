namespace EcommerceShop.BuildingBlocks.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ecommerce-shop-identity";
    public string Audience { get; set; } = "ecommerce-shop";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
