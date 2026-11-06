using DonationHub.Domain.Common;

namespace DonationHub.Application.Abstractions.Persistence;

public interface IGenericRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    IQueryable<TEntity> Query();
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Remove(TEntity entity);
}
