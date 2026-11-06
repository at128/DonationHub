using DonationHub.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DonationHub.Infrastructure.Data.Interceptors;

public class AuditingInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyChanges(eventData.Context);

        return base.SavingChangesAsync(
            eventData, result, cancellationToken);
    }

    private void ApplyChanges(DbContext? context)
    {
        if (context is null)
            return;

        var now = clock.GetUtcNow().UtcDateTime;

        // Convert hard delete to soft delete
        foreach (var entry in context.ChangeTracker
                     .Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;

            entry.Property(nameof(ISoftDeletable.IsDeleted))
                .CurrentValue = true;
            entry.Property(nameof(ISoftDeletable.DeletedAtUtc))
                .CurrentValue = entry.Entity.DeletedAtUtc ?? now;
        }

        // Set audit timestamps
        foreach (var entry in context.ChangeTracker
                     .Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(AuditableEntity.CreatedAt))
                    .CurrentValue = now;

                entry.Property(nameof(AuditableEntity.UpdatedAt))
                    .CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(AuditableEntity.UpdatedAt))
                    .CurrentValue = now;
            }
        }
    }
}
