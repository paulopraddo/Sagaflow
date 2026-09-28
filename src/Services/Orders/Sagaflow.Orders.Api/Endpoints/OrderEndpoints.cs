using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sagaflow.Contracts.Orders;
using Sagaflow.Orders.Api.Data;
using Sagaflow.Orders.Api.Domain;

namespace Sagaflow.Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", PlaceOrder);
        orders.MapGet("/{id:guid}", GetOrder).WithName(nameof(GetOrder));
        orders.MapGet("/", ListOrders);

        return app;
    }

    /// <summary>
    /// Accepts the order and returns immediately (202): the saga settles it asynchronously.
    /// Clients poll the Location header to follow its progress.
    /// </summary>
    private static async Task<Results<AcceptedAtRoute<OrderResponse>, ValidationProblem>> PlaceOrder(
        PlaceOrderRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        OrdersDbContext db,
        IPublishEndpoint publisher,
        OrderMetrics metrics,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (idempotencyKey is not null
            && await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct) is { } existing)
        {
            return Accepted(existing, sagaState: null);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, ct);

        var unknown = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = [.. unknown.Select(id => $"Product {id} does not exist.")],
            });
        }

        // Prices come from the local snapshot, never from the client.
        var items = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                var product = products[g.Key];
                return new OrderItem(product.ProductId, product.Sku, product.Name, g.Sum(i => i.Quantity), product.Price);
            })
            .ToList();

        var order = Order.Place(request.CustomerId, items, idempotencyKey, clock.GetUtcNow());
        db.Orders.Add(order);

        // Stored in the outbox and committed together with the order: the saga starts if and only if
        // the order exists.
        await publisher.Publish(new OrderSubmitted(
            order.Id,
            order.CustomerId,
            [.. order.Items.Select(i => new OrderLine(i.ProductId, i.Quantity, i.UnitPrice))],
            order.Total,
            order.CreatedAt), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two concurrent requests with the same idempotency key: the other one won.
            db.ChangeTracker.Clear();
            var winner = await db.Orders.AsNoTracking().FirstAsync(o => o.IdempotencyKey == idempotencyKey, ct);
            return Accepted(winner, sagaState: null);
        }

        metrics.OrderSubmitted();
        return Accepted(order, sagaState: null);
    }

    private static async Task<Results<Ok<OrderResponse>, NotFound>> GetOrder(Guid id, OrdersDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        var saga = await db.OrderSagas.AsNoTracking().FirstOrDefaultAsync(s => s.CorrelationId == id, ct);
        return TypedResults.Ok(OrderResponse.From(order, saga?.CurrentState));
    }

    private static async Task<Ok<List<OrderResponse>>> ListOrders(Guid? customerId, OrdersDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Orders.AsNoTracking()
            .Where(o => customerId == null || o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => OrderResponse.From(o, null))
            .ToListAsync(ct));

    private static AcceptedAtRoute<OrderResponse> Accepted(Order order, string? sagaState) =>
        TypedResults.AcceptedAtRoute(OrderResponse.From(order, sagaState), nameof(GetOrder), new { id = order.Id });
}

public sealed record PlaceOrderRequest(
    [property: Required] Guid CustomerId,
    [property: Required, MinLength(1), MaxLength(50)] IReadOnlyList<PlaceOrderItem> Items);

public sealed record PlaceOrderItem([property: Required] Guid ProductId, [property: Range(1, 1000)] int Quantity);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string? SagaState,
    decimal Total,
    string? CancellationReason,
    IReadOnlyList<OrderItem> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static OrderResponse From(Order o, string? sagaState) =>
        new(o.Id, o.CustomerId, o.Status.ToString(), sagaState, o.Total, o.CancellationReason, o.Items, o.CreatedAt, o.UpdatedAt);
}
