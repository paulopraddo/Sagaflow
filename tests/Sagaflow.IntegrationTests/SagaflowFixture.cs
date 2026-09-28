using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Sagaflow.Catalog.Api.Data;
using Sagaflow.Inventory.Api.Data;
using Sagaflow.Orders.Api.Data;
using Sagaflow.Payments.Api.Data;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(Sagaflow.IntegrationTests.SagaflowFixture))]

namespace Sagaflow.IntegrationTests;

/// <summary>
/// Starts real PostgreSQL and RabbitMQ containers once per test run and hosts all four services
/// in-process against them. Services talk to each other only through the broker, exactly as in production.
/// </summary>
public sealed class SagaflowFixture : IAsyncLifetime
{
    public const decimal DeclineAmountAbove = 1000m;
    public static readonly TimeSpan PaymentTimeout = TimeSpan.FromSeconds(20);

    private static readonly string[] Databases = ["catalog", "orders", "inventory", "payments"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    // Same image as docker-compose: RabbitMQ with the delayed-message exchange used for saga timeouts.
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("masstransit/rabbitmq:4.3.1").Build();

    private readonly List<IAsyncDisposable> _factories = [];

    public HttpClient Catalog { get; private set; } = default!;
    public HttpClient Orders { get; private set; } = default!;
    public HttpClient Inventory { get; private set; } = default!;
    public HttpClient Payments { get; private set; } = default!;
    public IServiceProvider InventoryServices { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());
        await CreateDatabasesAsync();

        Catalog = Start<CatalogDbContext>("catalog").CreateClient();
        Payments = Start<PaymentsDbContext>("payments").CreateClient();
        Orders = Start<OrdersDbContext>("orders").CreateClient();

        var inventory = Start<InventoryDbContext>("inventory");
        Inventory = inventory.CreateClient();
        InventoryServices = inventory.Services;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Any public type from a service assembly identifies its entry point, which sidesteps the
    /// ambiguity of four global <c>Program</c> classes.
    /// </summary>
    private WebApplicationFactory<TEntryPoint> Start<TEntryPoint>(string database)
        where TEntryPoint : class
    {
        var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting($"ConnectionStrings:{database}", ConnectionStringFor(database));
            host.UseSetting("ConnectionStrings:rabbitmq", _rabbitMq.GetConnectionString());
            host.UseSetting("PaymentGateway:DeclineAmountAbove", DeclineAmountAbove.ToString(System.Globalization.CultureInfo.InvariantCulture));
            host.UseSetting("PaymentGateway:Latency", "00:00:00.050");
            host.UseSetting("OrderSaga:PaymentTimeout", PaymentTimeout.ToString());
        });

        _factories.Add(factory);
        _ = factory.Server; // Boot now: runs migrations and starts the bus before any test sends traffic.
        return factory;
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task CreateDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        foreach (var database in Databases)
        {
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
