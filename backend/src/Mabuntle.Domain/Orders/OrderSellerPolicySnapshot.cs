namespace Mabuntle.Domain.Orders;

public sealed record OrderSellerPolicySnapshot(
    int? ReturnWindowDays,
    string? ReturnPolicy,
    string? ExchangePolicy,
    string? FulfilmentPolicy,
    string? SupportPolicy,
    string? CareInstructions,
    string? ProductDisclaimer,
    DateTimeOffset? SnapshotAtUtc);
