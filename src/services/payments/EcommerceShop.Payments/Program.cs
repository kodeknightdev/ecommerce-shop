using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.BuildingBlocks.Kafka;
using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Payments.Application;
using EcommerceShop.Payments.Data;
using EcommerceShop.Payments.Endpoints;
using EcommerceShop.Payments.Messaging;
using EcommerceShop.Payments.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("payments", typeof(PaymentProcessor));

builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("PaymentsDb")));
builder.Services.AddHealthChecks().AddDbContextCheck<PaymentsDbContext>("postgres", tags: new[] { HealthEndpoints.ReadyTag });
builder.Services.AddEcommerceShopJwt(builder.Configuration);
builder.Services.AddEcommerceShopKafka(builder.Configuration);

using (var loggerFactory = LoggerFactory.Create(l => l.AddConsole()))
{
    builder.Services.AddPaymentProvider(builder.Configuration, loggerFactory.CreateLogger("Payments"));
}

builder.Services.AddScoped<PaymentProcessor>();
builder.Services.AddOutboxRelay<PaymentsDbContext>(builder.Configuration);
builder.Services.AddHostedService<PaymentsConsumer>();

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapPaymentEndpoints();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

app.Run();

public partial class Program { }
