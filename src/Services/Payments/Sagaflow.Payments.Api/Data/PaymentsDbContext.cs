using Microsoft.EntityFrameworkCore;
using Sagaflow.Infrastructure;
using Sagaflow.Payments.Api.Domain;

namespace Sagaflow.Payments.Api.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(payment =>
        {
            payment.HasKey(p => p.Id);
            // One charge per order: the natural idempotency key.
            payment.HasIndex(p => p.OrderId).IsUnique();
            payment.Property(p => p.Amount).HasPrecision(18, 2);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            payment.Property(p => p.Reason).HasMaxLength(500);
        });

        modelBuilder.AddTransactionalOutbox();
    }
}
