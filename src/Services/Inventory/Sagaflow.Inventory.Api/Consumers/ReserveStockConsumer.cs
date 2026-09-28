using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Inventory;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Consumers;

/// <summary>
/// Reserves every line of an order or none of them. Concurrent reservations for the same product
/// are serialized by the xmin concurrency token: the loser throws, and MassTransit retries it
/// against fresh stock levels.
/// </summary>
public sealed class ReserveStockConsumer(InventoryDbContext db, TimeProvider clock, ILogger<ReserveStockConsumer> logger)
    : IConsumer<ReserveStock>
{
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        var command = context.Message;
        var ct = context.CancellationToken;

        if (await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == command.OrderId, ct) is { } existing)
        {
            // Redelivered command: replay the original decision instead of reserving twice.
            await PublishOutcome(context, existing);
            return;
        }

        var quantities = command.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var items = await db.Items
            .Where(i => quantities.Keys.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, ct);

        var shortages = quantities
            .Where(q => !items.TryGetValue(q.Key, out var item) || !item.CanReserve(q.Value))
            .Select(q => items.TryGetValue(q.Key, out var item)
                ? $"{item.Sku}: requested {q.Value}, available {item.Available}"
                : $"{q.Key}: unknown product")
            .ToList();

        StockReservation reservation;
        if (shortages.Count > 0)
        {
            reservation = StockReservation.Rejected(command.OrderId, "Insufficient stock (" + string.Join("; ", shortages) + ")", clock.GetUtcNow());
            logger.LogWarning("Stock reservation rejected for order {OrderId}: {Reason}", command.OrderId, reservation.Reason);
        }
        else
        {
            foreach (var (productId, quantity) in quantities)
            {
                items[productId].Reserve(quantity);
            }

            reservation = StockReservation.Accepted(
                command.OrderId,
                quantities.Select(q => new ReservationLine(q.Key, q.Value)),
                clock.GetUtcNow());
            logger.LogInformation("Stock reserved for order {OrderId}", command.OrderId);
        }

        db.Reservations.Add(reservation);
        await PublishOutcome(context, reservation);
        await db.SaveChangesAsync(ct);
    }

    private static Task PublishOutcome(ConsumeContext context, StockReservation reservation) =>
        reservation.Status == ReservationStatus.Rejected
            ? context.Publish(new StockReservationFailed(reservation.OrderId, reservation.Reason ?? "Rejected"))
            : context.Publish(new StockReserved(reservation.OrderId));
}
