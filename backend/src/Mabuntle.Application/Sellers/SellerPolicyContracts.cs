namespace Mabuntle.Application.Sellers;

public sealed record SellerPolicyResponse(
    int? ReturnWindowDays,
    string? ReturnPolicy,
    string? ExchangePolicy,
    string? FulfilmentPolicy,
    string? SupportPolicy,
    string? CareInstructions,
    string? ProductDisclaimer,
    bool IsComplete,
    IReadOnlyCollection<string> MissingFields,
    DateTimeOffset? UpdatedAtUtc);

public sealed record SellerPolicySnapshotResponse(
    int? ReturnWindowDays,
    string? ReturnPolicy,
    string? ExchangePolicy,
    string? FulfilmentPolicy,
    string? SupportPolicy,
    string? CareInstructions,
    string? ProductDisclaimer,
    DateTimeOffset? SnapshotAtUtc);
