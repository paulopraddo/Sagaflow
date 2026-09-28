using Boilerplate.Domain.Common;

namespace Boilerplate.Infrastructure.Persistence;

public sealed class UnitOfWork(BoilerplateDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
