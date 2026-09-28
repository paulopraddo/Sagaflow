namespace Sagaflow.Orders.Api.Domain;

public enum OrderStatus
{
    Pending,
    Completed,
    Cancelled,
}

/// <summary>
/// The order as the customer sees it. Its lifecycle is driven by <see cref="Saga.OrderStateMachine"/>;
/// this aggregate only records the outcome.
/// </summary>
public sealed class Order
{
    private Order() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public List<OrderItem> Items { get; private set; } = [];
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order Place(Guid customerId, IReadOnlyCollection<OrderItem> items, string? idempotencyKey, DateTimeOffset now)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("An order needs at least one item.", nameof(items));
        }

        return new Order
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            IdempotencyKey = idempotencyKey,
            Items = [.. items],
            Total = items.Sum(i => i.UnitPrice * i.Quantity),
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool Complete(DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending)
        {
            return false;
        }

        Status = OrderStatus.Completed;
        UpdatedAt = now;
        return true;
    }

    public bool Cancel(string reason, DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending)
        {
            return false;
        }

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAt = now;
        return true;
    }
}

public sealed record OrderItem(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice);
