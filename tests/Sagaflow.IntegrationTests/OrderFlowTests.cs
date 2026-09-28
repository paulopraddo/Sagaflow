using System.Net;
using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sagaflow.Contracts;
using Sagaflow.Contracts.Inventory;
using Sagaflow.Contracts.Orders;

namespace Sagaflow.IntegrationTests;

/// <summary>
/// End-to-end scenarios across Catalog, Inventory, Orders and Payments over real RabbitMQ and PostgreSQL.
/// </summary>
public sealed class OrderFlowTests(SagaflowFixture app)
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task Paid_order_completes_and_commits_stock()
    {
        var product = await CreateStockedProduct(price: 100m, stock: 10);

        var order = await PlaceOrder(product.Id, quantity: 2);
        var settled = await WaitForFinalStatus(order.Id);

        settled.Status.ShouldBe("Completed");
        settled.SagaState.ShouldBe("Completed");
        settled.Total.ShouldBe(200m);

        var stock = await Eventually(() => GetStock(product.Id), s => s.Reserved == 0);
        stock.Available.ShouldBe(8);

        var payment = await app.Payments.GetFromJsonAsync<PaymentDto>($"/payments/{order.Id}");
        payment!.Status.ShouldBe("Succeeded");
    }

    [Fact]
    public async Task Declined_payment_cancels_order_and_releases_stock()
    {
        // 2 × 600 exceeds the simulated gateway's decline threshold.
        var product = await CreateStockedProduct(price: 600m, stock: 5);

        var order = await PlaceOrder(product.Id, quantity: 2);
        var settled = await WaitForFinalStatus(order.Id);

        settled.Status.ShouldBe("Cancelled");
        settled.SagaState.ShouldBe("Cancelled");
        settled.CancellationReason.ShouldNotBeNull().ShouldContain("declined");

        // Compensation ran: nothing is left reserved and everything is back on sale.
        var stock = await GetStock(product.Id);
        stock.Available.ShouldBe(5);
        stock.Reserved.ShouldBe(0);

        var reservation = await app.Inventory.GetFromJsonAsync<ReservationDto>($"/inventory/reservations/{order.Id}");
        reservation!.Status.ShouldBe("Released");
    }

    [Fact]
    public async Task Insufficient_stock_cancels_order_without_charging()
    {
        var product = await CreateStockedProduct(price: 10m, stock: 1);

        var order = await PlaceOrder(product.Id, quantity: 3);
        var settled = await WaitForFinalStatus(order.Id);

        settled.Status.ShouldBe("Cancelled");
        settled.CancellationReason.ShouldNotBeNull().ShouldContain("Insufficient stock");

        (await app.Payments.GetAsync($"/payments/{order.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetStock(product.Id)).Available.ShouldBe(1);
    }

    [Fact]
    public async Task Retried_request_with_same_idempotency_key_creates_one_order()
    {
        var product = await CreateStockedProduct(price: 50m, stock: 10);
        var key = Guid.NewGuid().ToString();

        var first = await PlaceOrder(product.Id, quantity: 1, idempotencyKey: key);
        var second = await PlaceOrder(product.Id, quantity: 1, idempotencyKey: key);

        second.Id.ShouldBe(first.Id);
        await WaitForFinalStatus(first.Id);

        var stock = await Eventually(() => GetStock(product.Id), s => s.Reserved == 0);
        stock.Available.ShouldBe(9);
    }

    [Fact]
    public async Task Duplicate_reserve_command_reserves_stock_once()
    {
        var product = await CreateStockedProduct(price: 10m, stock: 10);
        var orderId = Guid.NewGuid();
        var command = new ReserveStock(orderId, [new OrderLine(product.Id, 4, 10m)]);

        // Two distinct message ids, so the inbox cannot dedupe them; the reservation record must.
        var bus = app.InventoryServices.GetRequiredService<IBus>();
        var endpoint = await bus.GetSendEndpoint(Queues.Address(Queues.ReserveStock));
        await endpoint.Send(command);
        await endpoint.Send(command);

        await Eventually(
            () => app.Inventory.GetAsync($"/inventory/reservations/{orderId}"),
            response => response.StatusCode == HttpStatusCode.OK);
        await Task.Delay(TimeSpan.FromSeconds(2)); // give the duplicate time to be processed

        var stock = await GetStock(product.Id);
        stock.Available.ShouldBe(6);
        stock.Reserved.ShouldBe(4);
    }

    private async Task<ProductDto> CreateStockedProduct(decimal price, int stock)
    {
        var sku = $"SKU-{Guid.NewGuid():N}"[..20];
        var created = await app.Catalog.PostAsJsonAsync("/products", new { sku, name = $"Product {sku}", price });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        // ProductCreated reaches Inventory asynchronously via the outbox.
        await Eventually(
            () => app.Inventory.PostAsJsonAsync($"/inventory/{product.Id}/restock", new { quantity = stock }),
            response => response.StatusCode == HttpStatusCode.OK);

        return product;
    }

    private async Task<OrderDto> PlaceOrder(Guid productId, int quantity, string? idempotencyKey = null)
    {
        // Orders learns about the product through its own projection, which may lag slightly behind.
        var response = await Eventually(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
                {
                    Content = JsonContent.Create(new { customerId = Guid.NewGuid(), items = new[] { new { productId, quantity } } }),
                };
                if (idempotencyKey is not null)
                {
                    request.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                return app.Orders.SendAsync(request);
            },
            r => r.StatusCode != HttpStatusCode.BadRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private Task<OrderDto> WaitForFinalStatus(Guid orderId) =>
        Eventually(
            async () => (await app.Orders.GetFromJsonAsync<OrderDto>($"/orders/{orderId}"))!,
            order => order.Status != "Pending");

    private async Task<StockDto> GetStock(Guid productId) =>
        (await app.Inventory.GetFromJsonAsync<StockDto>($"/inventory/{productId}"))!;

    private static async Task<T> Eventually<T>(Func<Task<T>> probe, Func<T, bool> condition)
    {
        using var timeout = new CancellationTokenSource(SettleTimeout);
        while (true)
        {
            var value = await probe();
            if (condition(value))
            {
                return value;
            }

            if (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Condition not met within {SettleTimeout}. Last value: {value}");
            }

            await Task.Delay(250, CancellationToken.None);
        }
    }

    private sealed record ProductDto(Guid Id, string Sku, decimal Price);

    private sealed record StockDto(Guid ProductId, int Available, int Reserved);

    private sealed record OrderDto(Guid Id, string Status, string? SagaState, decimal Total, string? CancellationReason);

    private sealed record PaymentDto(Guid OrderId, string Status);

    private sealed record ReservationDto(Guid OrderId, string Status);
}
