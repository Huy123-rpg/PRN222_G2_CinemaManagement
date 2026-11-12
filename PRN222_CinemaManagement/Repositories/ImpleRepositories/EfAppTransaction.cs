using Microsoft.EntityFrameworkCore.Storage;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public sealed class EfAppTransaction(IDbContextTransaction innerTransaction) : IAppTransaction
{
    public Task CommitAsync(CancellationToken ct = default)
        => innerTransaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default)
        => innerTransaction.RollbackAsync(ct);

    public ValueTask DisposeAsync()
        => innerTransaction.DisposeAsync();
}

