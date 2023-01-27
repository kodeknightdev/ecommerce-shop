using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.Identity.Auth;
using EcommerceShop.Identity.Data;
using EcommerceShop.Identity.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("identity", typeof(RegisterRequestValidator));

builder.Services.AddDbContext<IdentityDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("postgres", tags: new[] { HealthEndpoints.ReadyTag });

builder.Services.AddEcommerceShopJwt(builder.Configuration);
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapAuthEndpoints();
app.MapUserEndpoints();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await DbSeeder.MigrateAndSeedAsync(app.Services, app.Logger, seedDemoData: app.Environment.IsDevelopment());
}

app.Run();

public partial class Program { }
