namespace Sagaflow.Payments.Api.Domain;

public enum PaymentStatus
{
    Succeeded,
    Failed,
    Refunded,
}

public sealed class Payment
{
    private Payment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment FromCharge(Guid orderId, Guid customerId, decimal amount, ChargeResult result, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        CustomerId = customerId,
        Amount = amount,
        Status = result.Approved ? PaymentStatus.Succeeded : PaymentStatus.Failed,
        Reason = result.DeclineReason,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public bool Refund(string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Succeeded)
        {
            return false;
        }

        Status = PaymentStatus.Refunded;
        Reason = reason;
        UpdatedAt = now;
        return true;
    }
}
