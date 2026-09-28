using Boilerplate.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence;

public sealed class BoilerplateDbContext(DbContextOptions<BoilerplateDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoilerplateDbContext).Assembly);
    }
}
