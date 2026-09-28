using Sagaflow.Contracts.Orders;

namespace Sagaflow.Contracts.Inventory;

// Commands (sent point-to-point by the order saga)
public sealed record ReserveStock(Guid OrderId, IReadOnlyList<OrderLine> Lines);

public sealed record ReleaseStock(Guid OrderId, string Reason);

// Events (published by the inventory service)
public sealed record StockReserved(Guid OrderId);

public sealed record StockReservationFailed(Guid OrderId, string Reason);

public sealed record StockReleased(Guid OrderId);
