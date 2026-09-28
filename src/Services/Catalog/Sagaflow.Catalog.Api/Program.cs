using Sagaflow.Catalog.Api.Data;
using Sagaflow.Catalog.Api.Endpoints;
using Sagaflow.Infrastructure;
using Sagaflow.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPostgresDbContext<WebApplicationBuilder, CatalogDbContext>("catalog");
// Catalog only publishes events, so no consumers are registered.
builder.AddMessaging<WebApplicationBuilder, CatalogDbContext>(_ => { });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.AddValidation();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapProductEndpoints();

await app.MigrateDatabaseAsync<CatalogDbContext>();
await app.RunAsync();

public partial class Program;
