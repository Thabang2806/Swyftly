namespace Mabuntle.Api.Sellers;

public sealed record SellerModerationEventResponse(
    Guid AuditLogId,
    string ActionType,
    string? ActorRole,
    string? Reason,
    DateTimeOffset CreatedAtUtc);
