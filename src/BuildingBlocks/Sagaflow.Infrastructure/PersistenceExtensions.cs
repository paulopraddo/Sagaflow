using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sagaflow.Infrastructure;

public static class PersistenceExtensions
{
    public static TBuilder AddPostgresDbContext<TBuilder, TDbContext>(this TBuilder builder, string connectionName)
        where TBuilder : IHostApplicationBuilder
        where TDbContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' is missing.");

        builder.Services.AddDbContext<TDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres");

        return builder;
    }

    /// <summary>
    /// Adds the MassTransit inbox/outbox tables to a service's model.
    /// </summary>
    public static ModelBuilder AddTransactionalOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        return modelBuilder;
    }

    /// <summary>
    /// Applies pending EF Core migrations on startup. Convenient for a self-contained demo;
    /// a production deployment would run migrations as a separate pipeline step.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TDbContext>(this WebApplication app)
        where TDbContext : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PersistenceExtensions));

        logger.LogInformation("Applying migrations for {DbContext}", typeof(TDbContext).Name);
        await db.Database.MigrateAsync();
    }
}
