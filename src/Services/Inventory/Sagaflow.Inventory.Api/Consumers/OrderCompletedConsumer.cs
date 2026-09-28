using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Orders;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Consumers;

/// <summary>Once an order is paid its reserved units are committed (they leave stock for good).</summary>
public sealed class OrderCompletedConsumer(InventoryDbContext db, TimeProvider clock) : IConsumer<OrderCompleted>
{
    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        var ct = context.CancellationToken;
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.OrderId == context.Message.OrderId, ct);
        if (reservation is not { Status: ReservationStatus.Reserved })
        {
            return;
        }

        var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
        var items = await db.Items.Where(i => productIds.Contains(i.ProductId)).ToDictionaryAsync(i => i.ProductId, ct);

        foreach (var line in reservation.Lines)
        {
            if (items.TryGetValue(line.ProductId, out var item))
            {
                item.Commit(line.Quantity);
            }
        }

        reservation.MarkCommitted(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
