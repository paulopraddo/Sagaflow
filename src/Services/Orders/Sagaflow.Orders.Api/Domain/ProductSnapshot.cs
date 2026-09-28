namespace Sagaflow.Orders.Api.Domain;

/// <summary>
/// Local, eventually consistent copy of catalog data, fed by catalog events. Lets Orders price an
/// order without a synchronous call to Catalog, so a Catalog outage never blocks checkout.
/// </summary>
public sealed class ProductSnapshot
{
    private ProductSnapshot() { }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProductSnapshot Create(Guid productId, string sku, string name, decimal price, DateTimeOffset now) =>
        new() { ProductId = productId, Sku = sku, Name = name, Price = price, UpdatedAt = now };

    public void ChangePrice(decimal price, DateTimeOffset now)
    {
        Price = price;
        UpdatedAt = now;
    }
}
