using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks;
using EcommerceShop.Catalog.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("catalog", typeof(Program));
builder.Services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

var app = builder.Build();

app.UseServiceDefaults();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

public partial class Program { }
