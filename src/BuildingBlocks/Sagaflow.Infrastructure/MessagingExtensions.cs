using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sagaflow.Infrastructure;

public static class MessagingExtensions
{
    public const string RabbitMqConnectionName = "rabbitmq";

    /// <summary>
    /// Wires MassTransit over RabbitMQ with the transactional outbox/inbox backed by <typeparamref name="TDbContext"/>.
    /// <list type="bullet">
    /// <item>Bus outbox: messages published from API endpoints are stored in the same transaction as the
    /// business data and delivered by a background relay, so a crash never loses or invents a message.</item>
    /// <item>Consumer inbox/outbox: every receive endpoint deduplicates incoming messages and only
    /// dispatches outgoing ones after the consumer's database transaction commits.</item>
    /// </list>
    /// </summary>
    public static TBuilder AddMessaging<TBuilder, TDbContext>(
        this TBuilder builder,
        Action<IBusRegistrationConfigurator> configure)
        where TBuilder : IHostApplicationBuilder
        where TDbContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(RabbitMqConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{RabbitMqConnectionName}' is missing.");

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            // Middleware order matters: redelivery wraps retry, which wraps the outbox,
            // so each retry attempt runs in a fresh transaction.
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)));
                endpoint.UseMessageRetry(r => r.Intervals(100, 500, 1000));
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            // Scheduling uses RabbitMQ's delayed-message exchange (bundled in the masstransit/rabbitmq image).
            x.AddDelayedMessageScheduler();

            configure(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureHost(cfg, new Uri(connectionString));
                cfg.UseDelayedMessageScheduler();
                cfg.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    private static void ConfigureHost(IRabbitMqBusFactoryConfigurator cfg, Uri uri)
    {
        var virtualHost = uri.AbsolutePath.Trim('/') is { Length: > 0 } path ? Uri.UnescapeDataString(path) : "/";
        var credentials = uri.UserInfo.Split(':', 2);

        cfg.Host(uri.Host, (ushort)(uri.IsDefaultPort ? 5672 : uri.Port), virtualHost, host =>
        {
            if (credentials.Length == 2)
            {
                host.Username(Uri.UnescapeDataString(credentials[0]));
                host.Password(Uri.UnescapeDataString(credentials[1]));
            }
        });
    }
}
