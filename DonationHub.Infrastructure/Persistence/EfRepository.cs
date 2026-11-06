using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Domain.Common;
using DonationHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Persistence;

public sealed class EfRepository<TEntity>
    : IRepository<TEntity>
    where TEntity : Entity
{
    private readonly DbSet<TEntity> _dbSet;

    public EfRepository(AppDbContext context)
    {
        _dbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(
            new object[] { id }, ct);
    }

    public IQueryable<TEntity> Query()
    {
        return _dbSet.AsQueryable();
    }

    public async Task AddAsync(
        TEntity entity,
        CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    public void Remove(TEntity entity)
    {
        _dbSet.Remove(entity);
    }
}
