using MassTransit;
using Sagaflow.Infrastructure;
using Sagaflow.Orders.Api;
using Sagaflow.Orders.Api.Consumers;
using Sagaflow.Orders.Api.Data;
using Sagaflow.Orders.Api.Endpoints;
using Sagaflow.Orders.Api.Saga;
using Sagaflow.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPostgresDbContext<WebApplicationBuilder, OrdersDbContext>("orders");
builder.AddMessaging<WebApplicationBuilder, OrdersDbContext>(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(repository =>
        {
            // Same DbContext as the outbox, so saga state and outgoing messages commit together.
            repository.ExistingDbContext<OrdersDbContext>();
            repository.UsePostgres();
            repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
        })
        .Endpoint(e => e.Name = "orders-saga");

    x.AddConsumer<OrderStatusProjection>().Endpoint(e => e.Name = "orders-status-projection");
    x.AddConsumer<CatalogProjection>().Endpoint(e => e.Name = "orders-catalog-projection");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OrderMetrics>();
builder.Services.AddOptions<OrderSagaOptions>().BindConfiguration(OrderSagaOptions.SectionName);
builder.Services.AddOpenApi();
builder.Services.AddValidation();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapOrderEndpoints();

await app.MigrateDatabaseAsync<OrdersDbContext>();
await app.RunAsync();

public partial class Program;
