namespace Sagaflow.Contracts.Orders;

public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record OrderSubmitted(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    DateTimeOffset SubmittedAt);

public sealed record OrderCompleted(Guid OrderId, DateTimeOffset CompletedAt);

public sealed record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset CancelledAt);
