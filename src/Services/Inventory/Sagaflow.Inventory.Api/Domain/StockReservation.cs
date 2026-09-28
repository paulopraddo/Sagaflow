namespace Sagaflow.Inventory.Api.Domain;

public enum ReservationStatus
{
    Reserved,
    Rejected,
    Released,
    Committed,
}

/// <summary>
/// Records the outcome of a reservation request per order. Keyed by order id, it makes the
/// reserve/release handlers idempotent even beyond the inbox's deduplication window.
/// </summary>
public sealed class StockReservation
{
    private StockReservation() { }

    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public List<ReservationLine> Lines { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static StockReservation Accepted(Guid orderId, IEnumerable<ReservationLine> lines, DateTimeOffset now) =>
        new() { OrderId = orderId, Status = ReservationStatus.Reserved, Lines = [.. lines], CreatedAt = now, UpdatedAt = now };

    public static StockReservation Rejected(Guid orderId, string reason, DateTimeOffset now) =>
        new() { OrderId = orderId, Status = ReservationStatus.Rejected, Reason = reason, CreatedAt = now, UpdatedAt = now };

    public void MarkReleased(string reason, DateTimeOffset now)
    {
        Status = ReservationStatus.Released;
        Reason = reason;
        UpdatedAt = now;
    }

    public void MarkCommitted(DateTimeOffset now)
    {
        Status = ReservationStatus.Committed;
        UpdatedAt = now;
    }
}

public sealed record ReservationLine(Guid ProductId, int Quantity);
