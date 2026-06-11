using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mabuntle.Domain.Common;

namespace Mabuntle.Infrastructure.Persistence;

public sealed class AuditableEntitySaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SetAuditTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetAuditTimestamps(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var entry in dbContext.ChangeTracker.Entries<AuditableEntity>())
        {
            SetAuditTimestamps(entry, now);
        }
    }

    private static void SetAuditTimestamps(
        EntityEntry<AuditableEntity> entry,
        DateTimeOffset now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(entity => entity.CreatedAtUtc).CurrentValue = now;
            entry.Property(entity => entity.UpdatedAtUtc).CurrentValue = now;
            return;
        }

        if (entry.State == EntityState.Modified)
        {
            entry.Property(entity => entity.CreatedAtUtc).IsModified = false;
            entry.Property(entity => entity.UpdatedAtUtc).CurrentValue = now;
        }
    }
}
