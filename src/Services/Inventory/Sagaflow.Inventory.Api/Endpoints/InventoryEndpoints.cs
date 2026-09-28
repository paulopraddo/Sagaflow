using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventory = app.MapGroup("/inventory").WithTags("Inventory");

        inventory.MapGet("/", ListItems);
        inventory.MapGet("/{productId:guid}", GetItem);
        inventory.MapPost("/{productId:guid}/restock", Restock);
        inventory.MapGet("/reservations/{orderId:guid}", GetReservation);

        return app;
    }

    private static async Task<Ok<List<StockResponse>>> ListItems(InventoryDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Items.AsNoTracking()
            .OrderBy(i => i.Sku)
            .Select(i => new StockResponse(i.ProductId, i.Sku, i.Available, i.Reserved))
            .ToListAsync(ct));

    private static async Task<Results<Ok<StockResponse>, NotFound>> GetItem(Guid productId, InventoryDbContext db, CancellationToken ct) =>
        await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ProductId == productId, ct) is { } item
            ? TypedResults.Ok(StockResponse.From(item))
            : TypedResults.NotFound();

    private static async Task<Results<Ok<StockResponse>, NotFound>> Restock(
        Guid productId, RestockRequest request, InventoryDbContext db, CancellationToken ct)
    {
        if (await db.Items.FirstOrDefaultAsync(i => i.ProductId == productId, ct) is not { } item)
        {
            return TypedResults.NotFound();
        }

        item.Restock(request.Quantity);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(StockResponse.From(item));
    }

    private static async Task<Results<Ok<StockReservation>, NotFound>> GetReservation(Guid orderId, InventoryDbContext db, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId, ct) is { } reservation
            ? TypedResults.Ok(reservation)
            : TypedResults.NotFound();
}

public sealed record RestockRequest([property: Range(1, 100_000)] int Quantity);

public sealed record StockResponse(Guid ProductId, string Sku, int Available, int Reserved)
{
    public static StockResponse From(InventoryItem i) => new(i.ProductId, i.Sku, i.Available, i.Reserved);
}
