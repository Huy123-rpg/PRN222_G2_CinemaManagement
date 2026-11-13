namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>Quản lý transaction và lưu thay đổi chung trên cùng một DbContext trong phạm vi request.</summary>
public interface IUnitOfWork
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void ClearChangeTracker();
    void Detach<TEntity>(TEntity entity) where TEntity : class;
}

