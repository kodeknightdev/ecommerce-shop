using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.Catalog.Data;
using EcommerceShop.Catalog.Endpoints;
using EcommerceShop.Catalog.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("catalog", typeof(UpsertProductRequestValidator));

builder.Services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("postgres", tags: new[] { HealthEndpoints.ReadyTag });
builder.Services.AddEcommerceShopJwt(builder.Configuration);
builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapCategoryEndpoints();
app.MapProductEndpoints();
app.MapGrpcService<CatalogGrpcService>();
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await DbSeeder.MigrateAndSeedAsync(app.Services, app.Logger, seedDemoData: app.Environment.IsDevelopment());
}

app.Run();

public partial class Program { }
