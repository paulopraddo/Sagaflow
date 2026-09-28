using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sagaflow.Contracts.Inventory;
using Sagaflow.Contracts.Orders;
using Sagaflow.Contracts.Payments;
using Sagaflow.Orders.Api.Saga;

namespace Sagaflow.Orders.Tests;

/// <summary>
/// Exercises every transition of the order saga against MassTransit's in-memory test harness:
/// fast, deterministic, and no broker or database needed.
/// </summary>
public sealed class OrderStateMachineTests : IAsyncLifetime
{
    private static readonly TimeSpan PaymentTimeout = TimeSpan.FromSeconds(2);

    private readonly ServiceProvider _provider;
    private ITestHarness _harness = default!;
    private ISagaStateMachineTestHarness<OrderStateMachine, OrderState> _saga = default!;

    public OrderStateMachineTests()
    {
        _provider = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .Configure<OrderSagaOptions>(o => o.PaymentTimeout = PaymentTimeout)
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository();
                x.AddDelayedMessageScheduler();
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(validateScopes: true);
    }

    public async ValueTask InitializeAsync()
    {
        _harness = _provider.GetRequiredService<ITestHarness>();
        _harness.TestTimeout = TimeSpan.FromSeconds(10);
        await _harness.Start();
        _saga = _harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Submitted_order_reserves_stock()
    {
        var orderId = await SubmitOrder();

        (await _saga.Exists(orderId, s => s.ReservingStock)).ShouldNotBeNull();
        (await _harness.Sent.Any<ReserveStock>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Reserved_stock_triggers_payment()
    {
        var orderId = await SubmitOrder(total: 250m);
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);

        (await _harness.Sent.Any<ProcessPayment>(m => m.Context.Message.OrderId == orderId && m.Context.Message.Amount == 250m))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Successful_payment_completes_the_order()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);
        await Publish(new PaymentSucceeded(orderId, Guid.NewGuid()), s => s.Completed, orderId);

        (await _harness.Published.Any<OrderCompleted>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();
        (await _harness.Sent.Any<ReleaseStock>(m => m.Context.Message.OrderId == orderId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Failed_payment_releases_stock_then_cancels()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);
        await Publish(new PaymentFailed(orderId, "Card declined"), s => s.ReleasingStock, orderId);

        (await _harness.Sent.Any<ReleaseStock>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();

        await Publish(new StockReleased(orderId), s => s.Cancelled, orderId);

        (await _harness.Published.Any<OrderCancelled>(m =>
            m.Context.Message.OrderId == orderId && m.Context.Message.Reason == "Card declined")).ShouldBeTrue();
        (await _harness.Published.Any<OrderCompleted>(m => m.Context.Message.OrderId == orderId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Rejected_reservation_cancels_without_charging()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReservationFailed(orderId, "Insufficient stock"), s => s.Cancelled, orderId);

        (await _harness.Published.Any<OrderCancelled>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();
        (await _harness.Sent.Any<ProcessPayment>(m => m.Context.Message.OrderId == orderId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Payment_timeout_triggers_compensation()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);

        // No payment outcome arrives: the scheduled timeout must fire on its own.
        (await _saga.Exists(orderId, s => s.ReleasingStock, PaymentTimeout * 4)).ShouldNotBeNull();
        (await _harness.Sent.Any<ReleaseStock>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Payment_arriving_after_cancellation_is_refunded()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);
        await Publish(new PaymentFailed(orderId, "Timeout at PSP"), s => s.ReleasingStock, orderId);
        await Publish(new StockReleased(orderId), s => s.Cancelled, orderId);

        await _harness.Bus.Publish(new PaymentSucceeded(orderId, Guid.NewGuid()));

        (await _harness.Sent.Any<RefundPayment>(m => m.Context.Message.OrderId == orderId)).ShouldBeTrue();
        (await _saga.Exists(orderId, s => s.Cancelled)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Duplicated_events_are_ignored()
    {
        var orderId = await SubmitOrder();
        await Publish(new StockReserved(orderId), s => s.ProcessingPayment, orderId);
        await Publish(new PaymentSucceeded(orderId, Guid.NewGuid()), s => s.Completed, orderId);

        await _harness.Bus.Publish(new StockReserved(orderId));
        await _harness.Bus.Publish(new PaymentSucceeded(orderId, Guid.NewGuid()));

        await _harness.InactivityTask;

        (await _saga.Exists(orderId, s => s.Completed)).ShouldNotBeNull();
        (await _harness.Published.Any<Fault<PaymentSucceeded>>()).ShouldBeFalse();
        (await _harness.Published.Any<Fault<StockReserved>>()).ShouldBeFalse();
    }

    private async Task<Guid> SubmitOrder(decimal total = 100m)
    {
        var orderId = Guid.NewGuid();
        await _harness.Bus.Publish(new OrderSubmitted(
            orderId,
            Guid.NewGuid(),
            [new OrderLine(Guid.NewGuid(), 1, total)],
            total,
            DateTimeOffset.UtcNow));

        (await _saga.Exists(orderId, s => s.ReservingStock)).ShouldNotBeNull();
        return orderId;
    }

    private async Task Publish<T>(T message, Func<OrderStateMachine, State> expectedState, Guid orderId)
        where T : class
    {
        await _harness.Bus.Publish(message);
        (await _saga.Exists(orderId, expectedState)).ShouldNotBeNull($"saga did not reach the expected state after {typeof(T).Name}");
    }
}
