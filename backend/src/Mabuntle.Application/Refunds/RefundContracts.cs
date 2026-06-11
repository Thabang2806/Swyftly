using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Refunds;

public interface IRefundWorkflowService
{
    Task<Result<RefundResult>> CreateRefundRequestAsync(
        CreateRefundWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RefundResult>> ApproveRefundAsync(
        ApproveRefundWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RefundResult>> ConfirmManualProviderRefundAsync(
        ConfirmManualProviderRefundWorkflowRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateRefundWorkflowRequest(
    Guid OrderId,
    Guid? ReturnRequestId,
    decimal Amount,
    string Reason,
    Guid ActorUserId,
    string ActorRole,
    DateTimeOffset RequestedAtUtc);

public sealed record ApproveRefundWorkflowRequest(
    Guid RefundId,
    Guid ActorUserId,
    string ActorRole,
    string Reason,
    string? IpAddress,
    DateTimeOffset ApprovedAtUtc);

public sealed record ConfirmManualProviderRefundWorkflowRequest(
    Guid RefundId,
    Guid ActorUserId,
    string ActorRole,
    string ProviderRefundReference,
    string Reason,
    string? IpAddress,
    DateTimeOffset ConfirmedAtUtc);

public sealed record RefundResult(
    Guid RefundId,
    Guid OrderId,
    Guid PaymentId,
    Guid BuyerId,
    Guid SellerId,
    Guid? ReturnRequestId,
    decimal Amount,
    string Currency,
    string Status,
    string Reason,
    string? ProviderRefundReference,
    string? FailureReason,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RefundedAtUtc,
    IReadOnlyCollection<RefundEventResult> Events);

public sealed record RefundEventResult(
    Guid RefundEventId,
    string Status,
    string EventType,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record BuyerRefundResult(
    Guid RefundId,
    Guid OrderId,
    Guid? ReturnRequestId,
    decimal Amount,
    string Currency,
    string Status,
    string StatusMessage,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RefundedAtUtc,
    IReadOnlyCollection<BuyerRefundTimelineEventResult> Timeline);

public sealed record BuyerRefundTimelineEventResult(
    string Status,
    string EventType,
    string Message,
    DateTimeOffset CreatedAtUtc);
