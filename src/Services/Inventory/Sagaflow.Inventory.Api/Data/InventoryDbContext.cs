using Microsoft.EntityFrameworkCore;
using Sagaflow.Infrastructure;
using Sagaflow.Inventory.Api.Domain;

namespace Sagaflow.Inventory.Api.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> Items => Set<InventoryItem>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(item =>
        {
            item.HasKey(i => i.ProductId);
            item.Property(i => i.Sku).HasMaxLength(64);
            item.Property(i => i.Version).IsRowVersion();
            item.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Items_Available", "\"Available\" >= 0");
                t.HasCheckConstraint("CK_Items_Reserved", "\"Reserved\" >= 0");
            });
        });

        modelBuilder.Entity<StockReservation>(reservation =>
        {
            reservation.HasKey(r => r.OrderId);
            reservation.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            reservation.Property(r => r.Reason).HasMaxLength(500);
            reservation.OwnsMany(r => r.Lines, line => line.ToJson());
        });

        modelBuilder.AddTransactionalOutbox();
    }
}
