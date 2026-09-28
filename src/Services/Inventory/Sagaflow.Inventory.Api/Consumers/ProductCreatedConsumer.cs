using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Catalog;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Consumers;

/// <summary>Every catalog product gets a stock record, starting empty until it is restocked.</summary>
public sealed class ProductCreatedConsumer(InventoryDbContext db) : IConsumer<ProductCreated>
{
    public async Task Consume(ConsumeContext<ProductCreated> context)
    {
        var product = context.Message;
        if (await db.Items.AnyAsync(i => i.ProductId == product.ProductId, context.CancellationToken))
        {
            return;
        }

        db.Items.Add(InventoryItem.Create(product.ProductId, product.Sku));
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
