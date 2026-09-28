using Microsoft.EntityFrameworkCore;
using Sagaflow.Infrastructure;
using Sagaflow.Orders.Api.Domain;
using Sagaflow.Orders.Api.Saga;

namespace Sagaflow.Orders.Api.Data;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ProductSnapshot> Products => Set<ProductSnapshot>();
    public DbSet<OrderState> OrderSagas => Set<OrderState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.HasIndex(o => o.CustomerId);
            order.HasIndex(o => o.IdempotencyKey).IsUnique();
            order.Property(o => o.IdempotencyKey).HasMaxLength(100);
            order.Property(o => o.Total).HasPrecision(18, 2);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            order.Property(o => o.CancellationReason).HasMaxLength(500);
            order.OwnsMany(o => o.Items, item => item.ToJson());
        });

        modelBuilder.Entity<ProductSnapshot>(product =>
        {
            product.HasKey(p => p.ProductId);
            product.Property(p => p.Sku).HasMaxLength(64);
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrderState>(saga =>
        {
            saga.ToTable("OrderSaga");
            saga.HasKey(s => s.CorrelationId);
            saga.Property(s => s.CurrentState).HasMaxLength(32);
            saga.HasIndex(s => s.CurrentState);
            saga.Property(s => s.Total).HasPrecision(18, 2);
            saga.Property(s => s.FailureReason).HasMaxLength(500);
            saga.Property(s => s.Version).IsRowVersion();
        });

        modelBuilder.AddTransactionalOutbox();
    }
}
