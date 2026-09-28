using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts;
using Sagaflow.Infrastructure;
using Sagaflow.Payments.Api.Consumers;
using Sagaflow.Payments.Api.Data;
using Sagaflow.Payments.Api.Domain;
using Sagaflow.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPostgresDbContext<WebApplicationBuilder, PaymentsDbContext>("payments");
builder.AddMessaging<WebApplicationBuilder, PaymentsDbContext>(x =>
{
    x.AddConsumer<ProcessPaymentConsumer>().Endpoint(e => e.Name = Queues.ProcessPayment);
    x.AddConsumer<RefundPaymentConsumer>().Endpoint(e => e.Name = Queues.RefundPayment);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<PaymentGatewayOptions>()
    .BindConfiguration(PaymentGatewayOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/payments/{orderId:guid}", async (Guid orderId, PaymentsDbContext db, CancellationToken ct) =>
        await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId, ct) is { } payment
            ? Results.Ok(payment)
            : Results.NotFound())
    .WithTags("Payments");

await app.MigrateDatabaseAsync<PaymentsDbContext>();
await app.RunAsync();

public partial class Program;
