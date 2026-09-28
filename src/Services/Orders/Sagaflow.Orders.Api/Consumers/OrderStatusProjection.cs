using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Orders;
using Sagaflow.Orders.Api.Data;

namespace Sagaflow.Orders.Api.Consumers;

/// <summary>Reflects the saga's terminal events on the customer-facing <see cref="Domain.Order"/>.</summary>
public sealed class OrderStatusProjection(OrdersDbContext db, OrderMetrics metrics)
    : IConsumer<OrderCompleted>, IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == context.Message.OrderId, context.CancellationToken);
        if (order?.Complete(context.Message.CompletedAt) == true)
        {
            await db.SaveChangesAsync(context.CancellationToken);
            metrics.OrderCompleted(order.Total, context.Message.CompletedAt - order.CreatedAt);
        }
    }

    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == context.Message.OrderId, context.CancellationToken);
        if (order?.Cancel(context.Message.Reason, context.Message.CancelledAt) == true)
        {
            await db.SaveChangesAsync(context.CancellationToken);
            metrics.OrderCancelled(context.Message.CancelledAt - order.CreatedAt);
        }
    }
}
