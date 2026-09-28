using MassTransit;
using Microsoft.Extensions.Options;
using Sagaflow.Contracts;
using Sagaflow.Contracts.Inventory;
using Sagaflow.Contracts.Orders;
using Sagaflow.Contracts.Payments;

namespace Sagaflow.Orders.Api.Saga;

public sealed class OrderSagaOptions
{
    public const string SectionName = "OrderSaga";

    /// <summary>How long to wait for a payment outcome before cancelling the order.</summary>
    public TimeSpan PaymentTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Self-scheduled message that fires when the payment takes too long.</summary>
public sealed record PaymentTimeoutExpired(Guid OrderId);

/// <summary>
/// Orchestrates the order flow. Happy path:
/// <code>
/// OrderSubmitted → ReserveStock → StockReserved → ProcessPayment → PaymentSucceeded → OrderCompleted
/// </code>
/// Compensation: if payment fails or times out, the stock reservation is released before the order is
/// cancelled; a payment that succeeds after the timeout is refunded.
/// </summary>
public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State ReservingStock { get; private set; } = default!;
    public State ProcessingPayment { get; private set; } = default!;
    public State ReleasingStock { get; private set; } = default!;
    public State Completed { get; private set; } = default!;
    public State Cancelled { get; private set; } = default!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = default!;
    public Event<StockReserved> StockReserved { get; private set; } = default!;
    public Event<StockReservationFailed> StockReservationFailed { get; private set; } = default!;
    public Event<PaymentSucceeded> PaymentSucceeded { get; private set; } = default!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = default!;
    public Event<StockReleased> StockReleased { get; private set; } = default!;

    public Schedule<OrderState, PaymentTimeoutExpired> PaymentTimeout { get; private set; } = default!;

    public OrderStateMachine(IOptions<OrderSagaOptions> options, TimeProvider clock)
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReserved, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReservationFailed, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentSucceeded, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReleased, e => e.CorrelateById(m => m.Message.OrderId));

        Schedule(() => PaymentTimeout, saga => saga.PaymentTimeoutTokenId, schedule =>
        {
            schedule.Delay = options.Value.PaymentTimeout;
            schedule.Received = e => e.CorrelateById(m => m.Message.OrderId);
        });

        Initially(
            When(OrderSubmitted)
                .Then(ctx =>
                {
                    ctx.Saga.CustomerId = ctx.Message.CustomerId;
                    ctx.Saga.Total = ctx.Message.Total;
                    ctx.Saga.SubmittedAt = ctx.Message.SubmittedAt;
                })
                .Send(Queues.Address(Queues.ReserveStock), ctx => new ReserveStock(ctx.Message.OrderId, ctx.Message.Lines))
                .TransitionTo(ReservingStock));

        During(ReservingStock,
            When(StockReserved)
                .Send(Queues.Address(Queues.ProcessPayment), ctx => new ProcessPayment(ctx.Saga.CorrelationId, ctx.Saga.CustomerId, ctx.Saga.Total))
                .Schedule(PaymentTimeout, ctx => new PaymentTimeoutExpired(ctx.Saga.CorrelationId))
                .TransitionTo(ProcessingPayment),
            When(StockReservationFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, ctx.Message.Reason, clock.GetUtcNow()))
                .TransitionTo(Cancelled));

        During(ProcessingPayment,
            When(PaymentSucceeded)
                .Unschedule(PaymentTimeout)
                .Then(ctx => ctx.Saga.PaymentId = ctx.Message.PaymentId)
                .Publish(ctx => new OrderCompleted(ctx.Saga.CorrelationId, clock.GetUtcNow()))
                .TransitionTo(Completed),
            When(PaymentFailed)
                .Unschedule(PaymentTimeout)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Send(Queues.Address(Queues.ReleaseStock), ctx => new ReleaseStock(ctx.Saga.CorrelationId, ctx.Message.Reason))
                .TransitionTo(ReleasingStock),
            When(PaymentTimeout.Received)
                .Then(ctx => ctx.Saga.FailureReason = $"Payment not confirmed within {options.Value.PaymentTimeout.TotalSeconds:0}s")
                .Send(Queues.Address(Queues.ReleaseStock), ctx => new ReleaseStock(ctx.Saga.CorrelationId, ctx.Saga.FailureReason!))
                .TransitionTo(ReleasingStock));

        During(ReleasingStock,
            When(StockReleased)
                .Publish(ctx => new OrderCancelled(ctx.Saga.CorrelationId, ctx.Saga.FailureReason ?? "Cancelled", clock.GetUtcNow()))
                .TransitionTo(Cancelled));

        // A charge that lands after we already gave up on it must be given back.
        During(ReleasingStock, Cancelled,
            When(PaymentSucceeded)
                .Then(ctx => ctx.Saga.PaymentId = ctx.Message.PaymentId)
                .Send(Queues.Address(Queues.RefundPayment), ctx => new RefundPayment(ctx.Saga.CorrelationId, "Payment arrived after the order was cancelled")));

        // At-least-once delivery means late or duplicated events are normal; they must not fault.
        During(ReservingStock, Ignore(OrderSubmitted));
        During(ProcessingPayment, Ignore(OrderSubmitted), Ignore(StockReserved));
        During(ReleasingStock, Ignore(OrderSubmitted), Ignore(StockReserved), Ignore(PaymentFailed), Ignore(PaymentTimeout.Received));
        During(Completed, Ignore(OrderSubmitted), Ignore(StockReserved), Ignore(PaymentSucceeded), Ignore(PaymentTimeout.Received));
        During(Cancelled, Ignore(OrderSubmitted), Ignore(StockReserved), Ignore(StockReservationFailed), Ignore(PaymentFailed),
            Ignore(StockReleased), Ignore(PaymentTimeout.Received));

        WhenEnterAny(binder => binder.Then(ctx => ctx.Saga.UpdatedAt = clock.GetUtcNow()));
    }
}
