using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Disputes;

public interface IDisputeWorkflowService
{
    Task<Result<DisputeResult>> OpenDisputeAsync(
        OpenDisputeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DisputeResult>> AddMessageAsync(
        AddDisputeMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DisputeResult>> AddEvidenceAsync(
        AddDisputeEvidenceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DisputeResult>> ResolveAsync(
        ResolveDisputeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OpenDisputeRequest(
    Guid BuyerId,
    Guid BuyerUserId,
    Guid OrderId,
    Guid? ReturnRequestId,
    string Reason,
    IReadOnlyCollection<DisputeEvidenceInput> Evidence,
    DateTimeOffset OpenedAtUtc);

public sealed record AddDisputeMessageRequest(
    Guid DisputeId,
    Guid ActorProfileId,
    Guid ActorUserId,
    string ActorRole,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record AddDisputeEvidenceRequest(
    Guid DisputeId,
    Guid ActorProfileId,
    Guid ActorUserId,
    string ActorRole,
    DisputeEvidenceInput Evidence,
    DateTimeOffset CreatedAtUtc);

public sealed record ResolveDisputeRequest(
    Guid DisputeId,
    Guid ActorUserId,
    string ActorRole,
    string Outcome,
    string Reason,
    string? IpAddress,
    DateTimeOffset ResolvedAtUtc);

public sealed record DisputeEvidenceInput(
    string EvidenceType,
    string StorageReference,
    string? Description);

public sealed record DisputeResult(
    Guid DisputeId,
    Guid OrderId,
    Guid? ReturnRequestId,
    Guid BuyerId,
    Guid SellerId,
    string Status,
    string Reason,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolutionReason,
    IReadOnlyCollection<DisputeMessageResult> Messages,
    IReadOnlyCollection<DisputeEvidenceResult> Evidence);

public sealed record DisputeMessageResult(
    Guid DisputeMessageId,
    Guid SenderUserId,
    string SenderRole,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record DisputeEvidenceResult(
    Guid DisputeEvidenceId,
    Guid SubmittedByUserId,
    string SubmittedByRole,
    string EvidenceType,
    string StorageReference,
    string? Description,
    DateTimeOffset CreatedAtUtc);
