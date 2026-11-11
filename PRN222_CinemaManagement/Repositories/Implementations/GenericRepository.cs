using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class GenericRepository<TEntity>(CinemaDbContext context) : IGenericRepository<TEntity>
    where TEntity : class
{
    protected CinemaDbContext Context => context;
    protected DbSet<TEntity> DbSet => context.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        => await DbSet.AddRangeAsync(entities, ct);

    public void Update(TEntity entity)
        => DbSet.Update(entity);

    public void Remove(TEntity entity)
        => DbSet.Remove(entity);
}

