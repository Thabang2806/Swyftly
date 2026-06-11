namespace Mabuntle.Application.Admin;

public interface IAuditLogService
{
    Task RecordAsync(CreateAuditLogEntry entry, CancellationToken cancellationToken = default);
}

public sealed record CreateAuditLogEntry(
    string? ActorUserId,
    string? ActorRole,
    string ActionType,
    string EntityType,
    string? EntityId,
    string? PreviousValueJson = null,
    string? NewValueJson = null,
    string? Reason = null,
    string? IpAddress = null);
