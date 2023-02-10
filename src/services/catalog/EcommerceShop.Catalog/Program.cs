using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.Catalog.Data;
using EcommerceShop.Catalog.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("catalog", typeof(UpsertProductRequestValidator));

builder.Services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("postgres", tags: new[] { HealthEndpoints.ReadyTag });
builder.Services.AddEcommerceShopJwt(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapCategoryEndpoints();
app.MapProductEndpoints();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await DbSeeder.MigrateAndSeedAsync(app.Services, app.Logger, seedDemoData: app.Environment.IsDevelopment());
}

app.Run();

public partial class Program { }
