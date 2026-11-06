using DonationHub.Domain.Common;

namespace DonationHub.Application.Abstractions.Persistence;

// Compatibility name for existing consumers.
public interface IRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : Entity
{
}
