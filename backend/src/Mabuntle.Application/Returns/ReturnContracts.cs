using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Sellers;

namespace Mabuntle.Application.Returns;

public interface IReturnWorkflowService
{
    Task<Result<ReturnRequestResult>> RequestReturnAsync(
        CreateReturnRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnRequestResult>> ApproveReturnAsync(
        SellerReturnResponseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnRequestResult>> RejectReturnAsync(
        SellerReturnResponseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnRequestResult>> DisputeReturnAsync(
        BuyerReturnDisputeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateReturnRequest(
    Guid BuyerId,
    Guid BuyerUserId,
    Guid OrderId,
    string Reason,
    string? Details,
    IReadOnlyCollection<CreateReturnItemRequest> Items,
    DateTimeOffset RequestedAtUtc);

public sealed record CreateReturnItemRequest(
    Guid OrderItemId,
    int Quantity,
    string Reason,
    bool IsOpenedOrUnsealed,
    string? Note);

public sealed record SellerReturnResponseRequest(
    Guid SellerId,
    Guid SellerUserId,
    Guid ReturnRequestId,
    string? Message,
    DateTimeOffset RespondedAtUtc);

public sealed record BuyerReturnDisputeRequest(
    Guid BuyerId,
    Guid BuyerUserId,
    Guid ReturnRequestId,
    string Reason,
    DateTimeOffset DisputedAtUtc);

public sealed record ReturnRequestResult(
    Guid ReturnRequestId,
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string Status,
    string Reason,
    string? Details,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? SellerRespondedAtUtc,
    string? SellerResponseReason,
    DateTimeOffset? DisputedAtUtc,
    string? DisputeReason,
    IReadOnlyCollection<ReturnItemResult> Items,
    IReadOnlyCollection<ReturnMessageResult> Messages,
    SellerPolicySnapshotResponse? SellerPolicySnapshot = null);

public sealed record ReturnItemResult(
    Guid ReturnItemId,
    Guid OrderItemId,
    Guid ProductId,
    Guid ProductVariantId,
    int Quantity,
    string Reason,
    bool IsOpenedOrUnsealed,
    string? Note);

public sealed record ReturnMessageResult(
    Guid ReturnMessageId,
    Guid SenderUserId,
    string SenderRole,
    string Message,
    DateTimeOffset CreatedAtUtc);
