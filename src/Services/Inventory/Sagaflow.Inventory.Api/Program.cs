using Sagaflow.Contracts;
using Sagaflow.Infrastructure;
using Sagaflow.Inventory.Api.Consumers;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Endpoints;
using Sagaflow.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPostgresDbContext<WebApplicationBuilder, InventoryDbContext>("inventory");
builder.AddMessaging<WebApplicationBuilder, InventoryDbContext>(x =>
{
    // Commands get well-known queues so the order saga can address them directly.
    x.AddConsumer<ReserveStockConsumer>().Endpoint(e => e.Name = Queues.ReserveStock);
    x.AddConsumer<ReleaseStockConsumer>().Endpoint(e => e.Name = Queues.ReleaseStock);

    // Events are subscribed to by type; the endpoint name only needs to be unique per service.
    x.AddConsumer<ProductCreatedConsumer>().Endpoint(e => e.Name = "inventory-product-created");
    x.AddConsumer<OrderCompletedConsumer>().Endpoint(e => e.Name = "inventory-order-completed");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.AddValidation();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapInventoryEndpoints();

await app.MigrateDatabaseAsync<InventoryDbContext>();
await app.RunAsync();

public partial class Program;
