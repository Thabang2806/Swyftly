using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Admin;

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    public AuditLog(
        string? actorUserId,
        string? actorRole,
        string actionType,
        string entityType,
        string? entityId,
        DateTimeOffset createdAtUtc,
        string? previousValueJson = null,
        string? newValueJson = null,
        string? reason = null,
        string? ipAddress = null)
    {
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        ActionType = actionType;
        EntityType = entityType;
        EntityId = entityId;
        PreviousValueJson = previousValueJson;
        NewValueJson = newValueJson;
        Reason = reason;
        IpAddress = ipAddress;
        CreatedAtUtc = createdAtUtc;
    }

    public string? ActorUserId { get; private set; }

    public string? ActorRole { get; private set; }

    public string ActionType { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? PreviousValueJson { get; private set; }

    public string? NewValueJson { get; private set; }

    public string? Reason { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
