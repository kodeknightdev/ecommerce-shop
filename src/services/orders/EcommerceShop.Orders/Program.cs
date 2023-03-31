using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Correlation;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.BuildingBlocks.Kafka;
using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Orders.Application;
using EcommerceShop.Orders.Clients;
using EcommerceShop.Orders.Data;
using EcommerceShop.Orders.Endpoints;
using EcommerceShop.Orders.Messaging;
using EcommerceShop.Protos.Catalog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("orders", typeof(PlaceOrderRequestValidator));

builder.Services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("OrdersDb")));
builder.Services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>("postgres", tags: new[] { HealthEndpoints.ReadyTag });
builder.Services.AddEcommerceShopJwt(builder.Configuration);
builder.Services.AddEcommerceShopKafka(builder.Configuration);

builder.Services.AddScoped<PlaceOrderService>();
builder.Services.AddScoped<OrderSagaHandler>();
builder.Services.AddOutboxRelay<OrdersDbContext>(builder.Configuration);
builder.Services.AddHostedService<OrderEventsConsumer>();

// all outgoing calls go through ResiliencePolicies
var resilience = builder.Configuration.GetSection(ResilienceOptions.SectionName).Get<ResilienceOptions>() ?? new ResilienceOptions();
using var resilienceLogs = LoggerFactory.Create(l => l.AddConsole());

builder.Services
    .AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri(builder.Configuration["Services:CatalogGrpc"]!))
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
builder.Services.AddSingleton(new CatalogResilience(ResiliencePolicies.ForGrpc("catalog", resilience, resilienceLogs.CreateLogger("Resilience.Catalog"))));
builder.Services.AddScoped<ICatalogClient, GrpcCatalogClient>();

builder.Services
    .AddHttpClient<ICartClient, HttpCartClient>(c => c.BaseAddress = new Uri(builder.Configuration["Services:Cart"]!))
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddPolicyHandler(ResiliencePolicies.ForHttp("cart", resilience, resilienceLogs.CreateLogger("Resilience.Cart")));

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapOrderEndpoints();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
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
