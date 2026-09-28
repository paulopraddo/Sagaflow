namespace Sagaflow.Inventory.Api.Domain;

/// <summary>
/// Stock level of a single product. <see cref="Available"/> is what can still be sold;
/// <see cref="Reserved"/> is held for orders whose payment has not settled yet.
/// </summary>
public sealed class InventoryItem
{
    private InventoryItem() { }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public int Available { get; private set; }
    public int Reserved { get; private set; }

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL's xmin system column).</summary>
    public uint Version { get; private set; }

    public static InventoryItem Create(Guid productId, string sku) => new() { ProductId = productId, Sku = sku };

    public void Restock(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Available += quantity;
    }

    public bool CanReserve(int quantity) => quantity > 0 && Available >= quantity;

    public void Reserve(int quantity)
    {
        if (!CanReserve(quantity))
        {
            throw new InvalidOperationException($"Cannot reserve {quantity} units of {Sku}; only {Available} available.");
        }

        Available -= quantity;
        Reserved += quantity;
    }

    /// <summary>Compensation: puts reserved units back on sale.</summary>
    public void Release(int quantity)
    {
        var released = Math.Min(quantity, Reserved);
        Reserved -= released;
        Available += released;
    }

    /// <summary>The order was paid: reserved units leave the warehouse for good.</summary>
    public void Commit(int quantity) => Reserved -= Math.Min(quantity, Reserved);
}
