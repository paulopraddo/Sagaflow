using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Inventory;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Consumers;

/// <summary>
/// Compensating action of the order saga: returns reserved units to stock.
/// Always answers with <see cref="StockReleased"/> so the saga can finish, even when there was
/// nothing to release (reservation rejected, already released, or never received).
/// </summary>
public sealed class ReleaseStockConsumer(InventoryDbContext db, TimeProvider clock, ILogger<ReleaseStockConsumer> logger)
    : IConsumer<ReleaseStock>
{
    public async Task Consume(ConsumeContext<ReleaseStock> context)
    {
        var command = context.Message;
        var ct = context.CancellationToken;

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.OrderId == command.OrderId, ct);
        if (reservation is { Status: ReservationStatus.Reserved })
        {
            var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
            var items = await db.Items.Where(i => productIds.Contains(i.ProductId)).ToDictionaryAsync(i => i.ProductId, ct);

            foreach (var line in reservation.Lines)
            {
                if (items.TryGetValue(line.ProductId, out var item))
                {
                    item.Release(line.Quantity);
                }
            }

            reservation.MarkReleased(command.Reason, clock.GetUtcNow());
            logger.LogInformation("Stock released for order {OrderId}: {Reason}", command.OrderId, command.Reason);
        }

        await context.Publish(new StockReleased(command.OrderId));
        await db.SaveChangesAsync(ct);
    }
}
