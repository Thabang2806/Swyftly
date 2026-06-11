using Mabuntle.Application.Admin;
using Mabuntle.Domain.Admin;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Admin;

public sealed class EfAuditLogService(
    MabuntleDbContext dbContext,
    TimeProvider timeProvider) : IAuditLogService
{
    public async Task RecordAsync(CreateAuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog(
            entry.ActorUserId,
            entry.ActorRole,
            entry.ActionType,
            entry.EntityType,
            entry.EntityId,
            timeProvider.GetUtcNow(),
            entry.PreviousValueJson,
            entry.NewValueJson,
            entry.Reason,
            entry.IpAddress));

        await Task.CompletedTask;
    }
}
