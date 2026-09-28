using MassTransit;

namespace Sagaflow.Orders.Api.Saga;

/// <summary>Persisted state of one order saga instance. <see cref="CorrelationId"/> is the order id.</summary>
public sealed class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public Guid? PaymentId { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Token of the scheduled payment timeout, so it can be cancelled when payment settles.</summary>
    public Guid? PaymentTimeoutTokenId { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint Version { get; set; }
}
