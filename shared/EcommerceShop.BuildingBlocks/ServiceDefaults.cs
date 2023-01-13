using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using EcommerceShop.BuildingBlocks.Correlation;
using EcommerceShop.BuildingBlocks.Errors;
using EcommerceShop.BuildingBlocks.Logging;

namespace EcommerceShop.BuildingBlocks;

public static class ServiceDefaults
{
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName, Type validatorsAssemblyMarker)
    {
        builder.AddEcommerceShopLogging(serviceName);
        builder.Services.AddEcommerceShopProblemDetails();
        builder.Services.AddEcommerceShopSwagger($"ecommerce-shop {serviceName}");
        builder.Services.AddHealthChecks();
        builder.Services.AddValidatorsFromAssemblyContaining(validatorsAssemblyMarker);
        builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);
        app.UseEcommerceShopExceptionHandler();
        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }
}
