using Microsoft.EntityFrameworkCore;
using Sagaflow.Catalog.Api.Domain;
using Sagaflow.Infrastructure;

namespace Sagaflow.Catalog.Api.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.HasKey(p => p.Id);
            product.HasIndex(p => p.Sku).IsUnique();
            product.Property(p => p.Sku).HasMaxLength(64);
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Description).HasMaxLength(2000);
            product.Property(p => p.Price).HasPrecision(18, 2);
        });

        modelBuilder.AddTransactionalOutbox();
    }
}
