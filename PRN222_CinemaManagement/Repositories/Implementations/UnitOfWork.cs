using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class UnitOfWork(CinemaDbContext context) : IUnitOfWork
{
    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(ct);
        return new EfAppTransaction(transaction);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public void ClearChangeTracker()
        => context.ChangeTracker.Clear();

    public void Detach<TEntity>(TEntity entity) where TEntity : class
        => context.Entry(entity).State = EntityState.Detached;
}

