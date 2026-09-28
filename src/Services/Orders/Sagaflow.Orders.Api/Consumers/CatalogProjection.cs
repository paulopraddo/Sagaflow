using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sagaflow.Contracts.Catalog;
using Sagaflow.Orders.Api.Data;
using Sagaflow.Orders.Api.Domain;

namespace Sagaflow.Orders.Api.Consumers;

/// <summary>Keeps the local <see cref="ProductSnapshot"/> read model in sync with the catalog.</summary>
public sealed class CatalogProjection(OrdersDbContext db, TimeProvider clock)
    : IConsumer<ProductCreated>, IConsumer<ProductPriceChanged>
{
    public async Task Consume(ConsumeContext<ProductCreated> context)
    {
        var product = context.Message;
        if (await db.Products.AnyAsync(p => p.ProductId == product.ProductId, context.CancellationToken))
        {
            return;
        }

        db.Products.Add(ProductSnapshot.Create(product.ProductId, product.Sku, product.Name, product.Price, clock.GetUtcNow()));
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ProductPriceChanged> context)
    {
        var snapshot = await db.Products.FirstOrDefaultAsync(p => p.ProductId == context.Message.ProductId, context.CancellationToken);
        if (snapshot is null)
        {
            // Price change overtook creation; let redelivery try again once ProductCreated has landed.
            throw new InvalidOperationException($"Product {context.Message.ProductId} not projected yet.");
        }

        snapshot.ChangePrice(context.Message.NewPrice, clock.GetUtcNow());
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
