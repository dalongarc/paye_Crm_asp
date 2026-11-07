using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Paye.Core.Common;
using Paye.Core.Entities;

namespace Paye.Infrastructure.Data.Interceptors;

public sealed class AuditableEntitySaveChangesInterceptor(
    ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditAndSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditAndSoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var actor = currentUserService.GetActorIdentifier();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // 1. Convert physical delete of ISoftDeletable into soft delete
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = nowUtc;
                softDeletable.DeletedBy = actor;
            }

            // 2. Populate CreatedAt/By and UpdatedAt/By on AuditableEntity
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.CreatedAtUtc == default)
                    {
                        auditable.CreatedAtUtc = nowUtc;
                    }

                    if (string.IsNullOrWhiteSpace(auditable.CreatedBy))
                    {
                        auditable.CreatedBy = actor;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(nameof(AuditableEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;

                    auditable.UpdatedAtUtc = nowUtc;
                    auditable.UpdatedBy = actor;
                }
            }
        }
    }
}
