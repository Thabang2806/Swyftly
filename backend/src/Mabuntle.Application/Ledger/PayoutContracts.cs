using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Ledger;

public interface IPayoutAdministrationService
{
    Task<Result<SellerPayoutResult>> MakeAvailableAsync(
        PayoutMakeAvailableRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SellerPayoutResult>> HoldAsync(
        PayoutHoldRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SellerPayoutResult>> ReleaseAsync(
        PayoutReleaseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SellerPayoutResult>> ProcessAsync(
        PayoutProcessRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SellerPayoutResult>> ReconcileAsync(
        PayoutReconcileRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPayoutProvider
{
    string ProviderName { get; }

    Task<Result<PayoutProviderResult>> InitiatePayoutAsync(
        PayoutProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayoutProviderResult>> GetPayoutAsync(
        PayoutProviderStatusRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PayoutMakeAvailableRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutHoldRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutReleaseRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutProcessRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutReconcileRequest(
    Guid PayoutId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress);

public sealed record PayoutProviderRequest(
    Guid PayoutId,
    Guid SellerId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PayoutProviderStatusRequest(
    string ProviderPayoutReference);

public sealed record PayoutProviderResult(
    string Provider,
    string ProviderPayoutReference,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset ProcessedAtUtc,
    string? FailureReason);

public sealed class PayoutProviderOptions
{
    public const string SectionName = "PayoutProvider";

    public string ProviderName { get; set; } = "Fake";

    public string FakeOutcome { get; set; } = FakePayoutProviderOutcomes.PaidOut;
}

public static class FakePayoutProviderOutcomes
{
    public const string PaidOut = "PaidOut";

    public const string Processing = "Processing";

    public const string Failed = "Failed";
}

public sealed record SellerPayoutResult(
    Guid PayoutId,
    Guid SellerId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? HeldAtUtc,
    string? HoldReason,
    DateTimeOffset? ReleasedAtUtc,
    string? ReleaseReason,
    DateTimeOffset? AvailableAtUtc,
    DateTimeOffset? ProcessingAtUtc,
    DateTimeOffset? PaidOutAtUtc,
    DateTimeOffset? FailedAtUtc,
    string? FailureReason,
    string? ProviderName,
    string? ProviderPayoutReference);
