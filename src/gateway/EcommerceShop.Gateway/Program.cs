using EcommerceShop.BuildingBlocks.Auth;
using EcommerceShop.BuildingBlocks.Correlation;
using EcommerceShop.BuildingBlocks.Errors;
using EcommerceShop.BuildingBlocks.Health;
using EcommerceShop.BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.AddEcommerceShopLogging("gateway");
builder.Services.AddEcommerceShopProblemDetails();
builder.Services.AddEcommerceShopJwt(builder.Configuration);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(EcommerceShop.Contracts.HttpHeaderNames.CorrelationId)));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseEcommerceShopExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapEcommerceShopHealth();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapReverseProxy();

app.Run();

public partial class Program { }
