namespace Sagaflow.Contracts.Payments;

// Commands
public sealed record ProcessPayment(Guid OrderId, Guid CustomerId, decimal Amount);

public sealed record RefundPayment(Guid OrderId, string Reason);

// Events
public sealed record PaymentSucceeded(Guid OrderId, Guid PaymentId);

public sealed record PaymentFailed(Guid OrderId, string Reason);

public sealed record PaymentRefunded(Guid OrderId, Guid PaymentId);
