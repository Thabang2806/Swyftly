using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Ledger;
using Mabuntle.Application.Returns;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Inventory;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Returns;

public sealed class EfReturnWorkflowService(
    MabuntleDbContext dbContext,
    IPayoutAdministrationService payoutAdministrationService) : IReturnWorkflowService
{
    public async Task<Result<ReturnRequestResult>> RequestReturnAsync(
        CreateReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = ValidateCreateRequest(request);
        if (parsed.IsFailure)
        {
            return Result<ReturnRequestResult>.Failure(parsed.Error);
        }

        var order = await dbContext.Orders
            .Include(existing => existing.Items)
            .Include(existing => existing.StatusHistory)
            .SingleOrDefaultAsync(
                existing => existing.Id == request.OrderId && existing.BuyerId == request.BuyerId,
                cancellationToken);
        if (order is null)
        {
            return Result<ReturnRequestResult>.Failure(Error.NotFound("Returns.OrderNotFound", "Order was not found."));
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return Result<ReturnRequestResult>.Failure(
                Error.Conflict("Returns.OrderNotEligible", "Only delivered orders can be returned."));
        }

        var existingReturn = await dbContext.ReturnRequests
            .AnyAsync(
                returnRequest => returnRequest.OrderId == order.Id
                    && returnRequest.Status != ReturnStatus.Closed
                    && returnRequest.Status != ReturnStatus.Refunded,
                cancellationToken);
        if (existingReturn)
        {
            return Result<ReturnRequestResult>.Failure(
                Error.Conflict("Returns.ActiveReturnExists", "An active return already exists for this order."));
        }

        var returnRequest = new ReturnRequest(
            order.Id,
            order.BuyerId,
            order.SellerId,
            parsed.Value.ReturnReason,
            request.Details,
            request.RequestedAtUtc);

        foreach (var itemRequest in parsed.Value.Items)
        {
            var orderItem = order.Items.SingleOrDefault(item => item.Id == itemRequest.OrderItemId);
            if (orderItem is null)
            {
                return Validation("items", "One or more return items do not belong to the order.");
            }

            if (itemRequest.Quantity > orderItem.Quantity)
            {
                return Validation("items", "Return quantity cannot exceed the ordered quantity.");
            }

            returnRequest.AddItem(
                orderItem.Id,
                orderItem.ProductId,
                orderItem.ProductVariantId,
                itemRequest.Quantity,
                itemRequest.Reason,
                itemRequest.IsOpenedOrUnsealed,
                itemRequest.Note);
        }

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            returnRequest.AddMessage(request.BuyerUserId, "Buyer", request.Details, request.RequestedAtUtc);
        }

        returnRequest.MarkAwaitingSellerResponse(request.RequestedAtUtc);
        order.ChangeStatus(OrderStatus.ReturnRequested, request.RequestedAtUtc, "ReturnRequested");
        TrackLatestOrderStatusHistory(order);
        dbContext.ReturnRequests.Add(returnRequest);
        foreach (var returnItem in returnRequest.Items)
        {
            var snapshot = await InventoryMovementRecorder.LoadSnapshotAsync(
                dbContext,
                returnItem.ProductVariantId,
                cancellationToken);
            if (snapshot is not null)
            {
                dbContext.InventoryMovements.Add(InventoryMovementRecorder.CreateContext(
                    snapshot,
                    InventoryMovementType.ReturnRequested,
                    "ReturnWorkflow",
                    "Buyer requested a return; stock and reserved quantities were not changed automatically.",
                    actorUserId: request.BuyerUserId,
                    batchReference: null,
                    occurredAtUtc: request.RequestedAtUtc,
                    orderId: order.Id,
                    returnRequestId: returnRequest.Id));
            }
        }

        var payoutHoldResult = await HoldLinkedPayoutsAsync(
            order.Id,
            order.SellerId,
            request.BuyerUserId,
            returnRequest.Id,
            cancellationToken);
        if (payoutHoldResult.IsFailure)
        {
            return Result<ReturnRequestResult>.Failure(payoutHoldResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ReturnRequestResult>.Success(Map(returnRequest, MapSellerPolicySnapshot(order.SellerPolicySnapshot)));
    }

    public async Task<Result<ReturnRequestResult>> ApproveReturnAsync(
        SellerReturnResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var returnRequest = await GetSellerReturnAsync(request.SellerId, request.ReturnRequestId, cancellationToken);
        if (returnRequest is null)
        {
            return ReturnNotFound();
        }

        try
        {
            returnRequest.Approve(request.SellerUserId, request.Message, request.RespondedAtUtc);
            TrackLatestReturnMessage(returnRequest);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("returnRequest", exception.Message);
        }

        var order = await dbContext.Orders.SingleAsync(existing => existing.Id == returnRequest.OrderId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ReturnRequestResult>.Success(Map(returnRequest, MapSellerPolicySnapshot(order.SellerPolicySnapshot)));
    }

    public async Task<Result<ReturnRequestResult>> RejectReturnAsync(
        SellerReturnResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Validation("message", "A rejection reason is required.");
        }

        var returnRequest = await GetSellerReturnAsync(request.SellerId, request.ReturnRequestId, cancellationToken);
        if (returnRequest is null)
        {
            return ReturnNotFound();
        }

        try
        {
            returnRequest.Reject(request.SellerUserId, request.Message, request.RespondedAtUtc);
            TrackLatestReturnMessage(returnRequest);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("returnRequest", exception.Message);
        }

        var order = await dbContext.Orders.SingleAsync(existing => existing.Id == returnRequest.OrderId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ReturnRequestResult>.Success(Map(returnRequest, MapSellerPolicySnapshot(order.SellerPolicySnapshot)));
    }

    public async Task<Result<ReturnRequestResult>> DisputeReturnAsync(
        BuyerReturnDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Dispute reason is required.");
        }

        var returnRequest = await dbContext.ReturnRequests
            .Include(existing => existing.Items)
            .Include(existing => existing.Messages)
            .SingleOrDefaultAsync(
                existing => existing.Id == request.ReturnRequestId && existing.BuyerId == request.BuyerId,
                cancellationToken);
        if (returnRequest is null)
        {
            return ReturnNotFound();
        }

        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleAsync(existing => existing.Id == returnRequest.OrderId, cancellationToken);

        try
        {
            returnRequest.Dispute(request.BuyerUserId, request.Reason, request.DisputedAtUtc);
            order.ChangeStatus(OrderStatus.Disputed, request.DisputedAtUtc, "ReturnDisputed");
            TrackLatestReturnMessage(returnRequest);
            TrackLatestOrderStatusHistory(order);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("returnRequest", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ReturnRequestResult>.Success(Map(returnRequest, MapSellerPolicySnapshot(order.SellerPolicySnapshot)));
    }

    private async Task<Result> HoldLinkedPayoutsAsync(
        Guid orderId,
        Guid sellerId,
        Guid actorUserId,
        Guid returnRequestId,
        CancellationToken cancellationToken)
    {
        var payouts = await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .Where(payout => payout.SellerId == sellerId
                && payout.Items.Any(item => item.OrderId == orderId)
                && (payout.Status == SellerPayoutStatus.Pending || payout.Status == SellerPayoutStatus.Available))
            .ToListAsync(cancellationToken);

        foreach (var payout in payouts)
        {
            var result = await payoutAdministrationService.HoldAsync(
                new PayoutHoldRequest(
                    payout.Id,
                    actorUserId,
                    "Buyer",
                    $"Return request {returnRequestId} is active.",
                    null),
                cancellationToken);

            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }
        }

        return Result.Success();
    }

    private async Task<ReturnRequest?> GetSellerReturnAsync(
        Guid sellerId,
        Guid returnRequestId,
        CancellationToken cancellationToken) =>
        await dbContext.ReturnRequests
            .Include(existing => existing.Items)
            .Include(existing => existing.Messages)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.SellerId == sellerId,
                cancellationToken);

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private void TrackLatestReturnMessage(ReturnRequest returnRequest)
    {
        var message = returnRequest.Messages.LastOrDefault();
        if (message is not null)
        {
            dbContext.ReturnMessages.Add(message);
        }
    }

    private static Result<ParsedCreateReturnRequest> ValidateCreateRequest(CreateReturnRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.BuyerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerId", "Buyer id is required."));
        }

        if (request.BuyerUserId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerUserId", "Buyer user id is required."));
        }

        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (!Enum.TryParse<ReturnReason>(request.Reason, ignoreCase: true, out var returnReason))
        {
            failures.Add(new ValidationFailure("reason", "Return reason is invalid."));
        }

        if (request.Items.Count == 0)
        {
            failures.Add(new ValidationFailure("items", "At least one return item is required."));
        }

        var parsedItems = new List<ParsedCreateReturnItemRequest>();
        foreach (var item in request.Items)
        {
            if (item.OrderItemId == Guid.Empty)
            {
                failures.Add(new ValidationFailure("items", "Order item id is required."));
            }

            if (item.Quantity <= 0)
            {
                failures.Add(new ValidationFailure("items", "Quantity must be positive."));
            }

            if (!Enum.TryParse<ReturnReason>(item.Reason, ignoreCase: true, out var itemReason))
            {
                failures.Add(new ValidationFailure("items", "Return item reason is invalid."));
                continue;
            }

            if (item.IsOpenedOrUnsealed && itemReason == ReturnReason.ChangedMind)
            {
                failures.Add(new ValidationFailure(
                    "items",
                    "Opened or unsealed items cannot be returned for changed-mind reasons."));
            }

            parsedItems.Add(new ParsedCreateReturnItemRequest(
                item.OrderItemId,
                item.Quantity,
                itemReason,
                item.IsOpenedOrUnsealed,
                item.Note));
        }

        return failures.Count == 0
            ? Result<ParsedCreateReturnRequest>.Success(new ParsedCreateReturnRequest(returnReason, parsedItems))
            : Result<ParsedCreateReturnRequest>.Failure(Error.Validation(failures));
    }

    private static Result<ReturnRequestResult> ReturnNotFound() =>
        Result<ReturnRequestResult>.Failure(
            Error.NotFound("Returns.NotFound", "Return request was not found."));

    private static Result<ReturnRequestResult> Validation(string propertyName, string message) =>
        Result<ReturnRequestResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

    public static ReturnRequestResult Map(
        ReturnRequest returnRequest,
        SellerPolicySnapshotResponse? sellerPolicySnapshot = null) =>
        new(
            returnRequest.Id,
            returnRequest.OrderId,
            returnRequest.BuyerId,
            returnRequest.SellerId,
            returnRequest.Status.ToString(),
            returnRequest.Reason.ToString(),
            returnRequest.Details,
            returnRequest.RequestedAtUtc,
            returnRequest.SellerRespondedAtUtc,
            returnRequest.SellerResponseReason,
            returnRequest.DisputedAtUtc,
            returnRequest.DisputeReason,
            returnRequest.Items
                .OrderBy(item => item.Id)
                .Select(item => new ReturnItemResult(
                    item.Id,
                    item.OrderItemId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.Quantity,
                    item.Reason.ToString(),
                    item.IsOpenedOrUnsealed,
                    item.Note))
                .ToArray(),
            returnRequest.Messages
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => new ReturnMessageResult(
                    message.Id,
                    message.SenderUserId,
                    message.SenderRole,
                    message.Message,
                    message.CreatedAtUtc))
                .ToArray(),
            sellerPolicySnapshot);

    private static SellerPolicySnapshotResponse? MapSellerPolicySnapshot(OrderSellerPolicySnapshot? snapshot) =>
        snapshot is null
            ? null
            : new SellerPolicySnapshotResponse(
                snapshot.ReturnWindowDays,
                snapshot.ReturnPolicy,
                snapshot.ExchangePolicy,
                snapshot.FulfilmentPolicy,
                snapshot.SupportPolicy,
                snapshot.CareInstructions,
                snapshot.ProductDisclaimer,
                snapshot.SnapshotAtUtc);

    private sealed record ParsedCreateReturnRequest(
        ReturnReason ReturnReason,
        IReadOnlyCollection<ParsedCreateReturnItemRequest> Items);

    private sealed record ParsedCreateReturnItemRequest(
        Guid OrderItemId,
        int Quantity,
        ReturnReason Reason,
        bool IsOpenedOrUnsealed,
        string? Note);
}
