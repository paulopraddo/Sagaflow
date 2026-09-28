namespace Sagaflow.Catalog.Api.Domain;

public sealed class Product
{
    private Product() { }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Product Create(string sku, string name, string? description, decimal price, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        var now = clock.GetUtcNow();

        return new Product
        {
            Id = Guid.CreateVersion7(),
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = price,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void ChangePrice(decimal newPrice, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newPrice);
        Price = newPrice;
        UpdatedAt = clock.GetUtcNow();
    }
}
