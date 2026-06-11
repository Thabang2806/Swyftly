using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Payments;
using Mabuntle.Application.Refunds;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Payments;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Refunds;

public sealed class EfRefundWorkflowService(
    MabuntleDbContext dbContext,
    IPaymentProvider paymentProvider,
    IAuditLogService auditLogService) : IRefundWorkflowService
{
    public async Task<Result<RefundResult>> CreateRefundRequestAsync(
        CreateRefundWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation.Count > 0)
        {
            return Result<RefundResult>.Failure(Error.Validation(validation));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleOrDefaultAsync(existing => existing.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.OrderNotFound", "Order was not found."));
        }

        var payment = await dbContext.Payments
            .SingleOrDefaultAsync(
                existing => existing.OrderId == order.Id && existing.Status != PaymentStatus.Failed,
                cancellationToken);
        if (payment is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.PaymentNotFound", "Payment was not found."));
        }

        if (payment.Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            return Result<RefundResult>.Failure(
                Error.Conflict("Refunds.PaymentNotRefundable", "Only paid payments can be refunded."));
        }

        var totalAlreadyRefunded = await TotalRefundedOrPendingAsync(payment.Id, null, cancellationToken);
        if (totalAlreadyRefunded + request.Amount > payment.Amount)
        {
            return Validation("amount", "Refund amount cannot exceed the remaining refundable payment amount.");
        }

        ReturnRequest? returnRequest = null;
        if (request.ReturnRequestId.HasValue)
        {
            returnRequest = await dbContext.ReturnRequests
                .SingleOrDefaultAsync(
                    existing => existing.Id == request.ReturnRequestId.Value && existing.OrderId == order.Id,
                    cancellationToken);
            if (returnRequest is null)
            {
                return Result<RefundResult>.Failure(Error.NotFound("Refunds.ReturnNotFound", "Return request was not found."));
            }

            try
            {
                if (returnRequest.Status != ReturnStatus.RefundPending)
                {
                    returnRequest.MarkRefundPending(request.RequestedAtUtc);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Validation("returnRequest", exception.Message);
            }
        }

        var refund = new Refund(
            order.Id,
            payment.Id,
            order.BuyerId,
            order.SellerId,
            request.ReturnRequestId,
            request.Amount,
            payment.Currency,
            request.Reason,
            request.ActorUserId,
            request.ActorRole,
            request.RequestedAtUtc);

        dbContext.Refunds.Add(refund);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RefundResult>.Success(Map(refund));
    }

    public async Task<Result<RefundResult>> ApproveRefundAsync(
        ApproveRefundWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Approval reason is required.");
        }

        var refund = await dbContext.Refunds
            .Include(existing => existing.Events)
            .SingleOrDefaultAsync(existing => existing.Id == request.RefundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.NotFound", "Refund was not found."));
        }

        if (refund.Status == RefundStatus.Refunded)
        {
            return Result<RefundResult>.Success(Map(refund));
        }

        if (refund.Status == RefundStatus.Processing)
        {
            return Result<RefundResult>.Failure(Error.Conflict(
                "Refunds.AlreadyProcessing",
                "Refund processing has already started. Manual reconciliation is required before retrying approval."));
        }

        if (refund.RequestedByUserId == request.ActorUserId)
        {
            return Result<RefundResult>.Failure(Error.Forbidden(
                "Refunds.DualControlRequired",
                "The user who created a refund request cannot approve the same refund."));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.Items)
            .Include(existing => existing.StatusHistory)
            .SingleAsync(existing => existing.Id == refund.OrderId, cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(existing => existing.Id == refund.PaymentId, cancellationToken);
        var returnRequest = refund.ReturnRequestId.HasValue
            ? await dbContext.ReturnRequests
                .Include(existing => existing.Items)
                .SingleOrDefaultAsync(existing => existing.Id == refund.ReturnRequestId.Value, cancellationToken)
            : null;

        try
        {
            refund.Approve(request.ActorUserId, request.Reason, request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            refund.MarkProcessing(request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("refund", exception.Message);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<RefundResult>.Failure(Error.Conflict(
                "Refunds.ConcurrentApproval",
                "Refund approval was already changed by another process."));
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderReference))
        {
            refund.MarkFailed("Payment provider reference is missing.", request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Validation("payment", "Payment provider reference is missing.");
        }

        var providerResult = await paymentProvider.RefundPaymentAsync(
            new PaymentRefundRequest(
                payment.ProviderReference,
                refund.Amount,
                refund.Currency,
                request.Reason,
                refund.Id.ToString("N"),
                new Dictionary<string, string>
                {
                    ["refundId"] = refund.Id.ToString(),
                    ["orderId"] = refund.OrderId.ToString()
                }),
            cancellationToken);

        if (providerResult.IsFailure)
        {
            if (providerResult.Error.Code == PayFastPaymentProvider.ManualRefundRequiredCode)
            {
                try
                {
                    refund.MarkProviderActionRequired(providerResult.Error.Description, request.ApprovedAtUtc);
                    TrackLatestRefundEvent(refund);
                }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
                {
                    return Validation("refund", exception.Message);
                }

                await auditLogService.RecordAsync(
                    new CreateAuditLogEntry(
                        request.ActorUserId.ToString(),
                        request.ActorRole,
                        "RefundProviderActionRequired",
                        "Refund",
                        refund.Id.ToString(),
                        JsonSerializer.Serialize(new { status = RefundStatus.Requested.ToString() }),
                        JsonSerializer.Serialize(new { status = RefundStatus.Processing.ToString(), amount = refund.Amount, provider = payment.Provider }),
                        request.Reason,
                        request.IpAddress),
                    cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
                return Result<RefundResult>.Success(Map(refund));
            }

            refund.MarkFailed(providerResult.Error.Description, request.ApprovedAtUtc);
            TrackLatestRefundEvent(refund);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<RefundResult>.Failure(providerResult.Error);
        }

        var providerRefund = providerResult.Value;
        var completionResult = await CompleteRefundAccountingAsync(
            refund,
            payment,
            order,
            returnRequest,
            providerRefund.ProviderRefundReference,
            providerRefund.RefundedAtUtc,
            cancellationToken);
        if (completionResult.IsFailure)
        {
            return completionResult;
        }

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                request.ActorUserId.ToString(),
                request.ActorRole,
                "RefundApproved",
                "Refund",
                refund.Id.ToString(),
                JsonSerializer.Serialize(new { status = RefundStatus.Requested.ToString() }),
                JsonSerializer.Serialize(new { status = RefundStatus.Refunded.ToString(), amount = refund.Amount }),
                request.Reason,
                request.IpAddress),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RefundResult>.Success(Map(refund));
    }

    public async Task<Result<RefundResult>> ConfirmManualProviderRefundAsync(
        ConfirmManualProviderRefundWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateManualProviderConfirmationRequest(request);
        if (validation.Count > 0)
        {
            return Result<RefundResult>.Failure(Error.Validation(validation));
        }

        var refund = await dbContext.Refunds
            .Include(existing => existing.Events)
            .SingleOrDefaultAsync(existing => existing.Id == request.RefundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundResult>.Failure(Error.NotFound("Refunds.NotFound", "Refund was not found."));
        }

        if (refund.Status == RefundStatus.Refunded)
        {
            return Result<RefundResult>.Success(Map(refund));
        }

        if (refund.Status != RefundStatus.Processing)
        {
            return Result<RefundResult>.Failure(Error.Conflict(
                "Refunds.NotAwaitingManualProviderConfirmation",
                "Only processing refunds can be manually confirmed after a provider refund."));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.Items)
            .Include(existing => existing.StatusHistory)
            .SingleAsync(existing => existing.Id == refund.OrderId, cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(existing => existing.Id == refund.PaymentId, cancellationToken);
        var returnRequest = refund.ReturnRequestId.HasValue
            ? await dbContext.ReturnRequests
                .Include(existing => existing.Items)
                .SingleOrDefaultAsync(existing => existing.Id == refund.ReturnRequestId.Value, cancellationToken)
            : null;

        var completionResult = await CompleteRefundAccountingAsync(
            refund,
            payment,
            order,
            returnRequest,
            request.ProviderRefundReference,
            request.ConfirmedAtUtc,
            cancellationToken);
        if (completionResult.IsFailure)
        {
            return completionResult;
        }

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                request.ActorUserId.ToString(),
                request.ActorRole,
                "ManualProviderRefundConfirmed",
                "Refund",
                refund.Id.ToString(),
                JsonSerializer.Serialize(new { status = RefundStatus.Processing.ToString() }),
                JsonSerializer.Serialize(new { status = RefundStatus.Refunded.ToString(), amount = refund.Amount, providerRefundReference = request.ProviderRefundReference }),
                request.Reason,
                request.IpAddress),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RefundResult>.Success(Map(refund));
    }

    private async Task<Result<RefundResult>> CompleteRefundAccountingAsync(
        Refund refund,
        Payment payment,
        Order order,
        ReturnRequest? returnRequest,
        string providerRefundReference,
        DateTimeOffset refundedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            refund.MarkRefunded(providerRefundReference, refundedAtUtc);
            TrackLatestRefundEvent(refund);
            await CreateLedgerReversalsAndAdjustBalancesAsync(refund, payment, order, refundedAtUtc, cancellationToken);
            var totalRefunded = await TotalRefundedOrPendingAsync(payment.Id, refund.Id, cancellationToken) + refund.Amount;
            payment.ApplyRefund(totalRefunded, refundedAtUtc);
            if (payment.Status == PaymentStatus.Refunded)
            {
                order.ChangeStatus(OrderStatus.Refunded, refundedAtUtc, "RefundApproved");
                TrackLatestOrderStatusHistory(order);
            }

            if (returnRequest is not null)
            {
                returnRequest.MarkRefunded(refundedAtUtc);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("refund", exception.Message);
        }

        await RecordRefundCompletedInventoryMovementsAsync(
            refund,
            payment,
            order,
            returnRequest,
            refundedAtUtc,
            cancellationToken);

        return Result<RefundResult>.Success(Map(refund));
    }

    private async Task RecordRefundCompletedInventoryMovementsAsync(
        Refund refund,
        Payment payment,
        Order order,
        ReturnRequest? returnRequest,
        DateTimeOffset refundedAtUtc,
        CancellationToken cancellationToken)
    {
        var variantIds = returnRequest is not null
            ? returnRequest.Items.Select(item => item.ProductVariantId).Distinct().ToArray()
            : order.Items.Select(item => item.ProductVariantId).Distinct().ToArray();

        foreach (var variantId in variantIds)
        {
            var alreadyRecorded = await dbContext.InventoryMovements.AnyAsync(
                movement => movement.MovementType == InventoryMovementType.RefundCompleted
                    && movement.RefundId == refund.Id
                    && movement.ProductVariantId == variantId,
                cancellationToken);
            if (alreadyRecorded)
            {
                continue;
            }

            var snapshot = await InventoryMovementRecorder.LoadSnapshotAsync(dbContext, variantId, cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            dbContext.InventoryMovements.Add(InventoryMovementRecorder.CreateContext(
                snapshot,
                InventoryMovementType.RefundCompleted,
                "RefundWorkflow",
                "Refund completed; stock and reserved quantities were not changed automatically.",
                refund.ApprovedByUserId,
                batchReference: null,
                occurredAtUtc: refundedAtUtc,
                orderId: order.Id,
                paymentId: payment.Id,
                returnRequestId: returnRequest?.Id,
                refundId: refund.Id));
        }
    }

    private async Task CreateLedgerReversalsAndAdjustBalancesAsync(
        Refund refund,
        Payment payment,
        Order order,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var paymentEntries = await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == payment.Id)
            .ToListAsync(cancellationToken);
        var ratio = refund.Amount / payment.Amount;
        var sellerOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.SellerPendingBalanceCredited)
            .Sum(entry => entry.Amount);
        var platformOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.PlatformCommissionRecorded)
            .Sum(entry => entry.Amount);
        var providerFeeOriginal = paymentEntries
            .Where(entry => entry.Type == LedgerEntryType.PaymentProviderFeeRecorded)
            .Sum(entry => entry.Amount);
        var sellerDebit = RoundMoney(sellerOriginal * ratio);
        var platformDebit = RoundMoney(platformOriginal * ratio);
        var providerFeeCredit = RoundMoney(providerFeeOriginal * ratio);

        var refundIssuedEntry = new LedgerEntry(
            order.Id,
            null,
            order.SellerId,
            order.BuyerId,
            payment.Id,
            LedgerEntryType.RefundIssued,
            refund.Amount,
            refund.Currency,
            LedgerDirection.Debit,
            $"Refund issued for refund {refund.Id}.",
            createdAtUtc);
        var sellerRefundReversalEntry = new LedgerEntry(
            order.Id,
            null,
            order.SellerId,
            order.BuyerId,
            payment.Id,
            LedgerEntryType.RefundReversal,
            sellerDebit,
            refund.Currency,
            LedgerDirection.Debit,
            "Seller balance refund reversal.",
            createdAtUtc);
        var platformRefundReversalEntry = new LedgerEntry(
            order.Id,
            null,
            order.SellerId,
            order.BuyerId,
            payment.Id,
            LedgerEntryType.RefundReversal,
            platformDebit,
            refund.Currency,
            LedgerDirection.Debit,
            "Platform commission refund reversal.",
            createdAtUtc);
        var providerFeeRefundReversalEntry = new LedgerEntry(
            order.Id,
            null,
            order.SellerId,
            order.BuyerId,
            payment.Id,
            LedgerEntryType.RefundReversal,
            providerFeeCredit,
            refund.Currency,
            LedgerDirection.Credit,
            "Payment provider fee refund reversal.",
            createdAtUtc);

        dbContext.LedgerEntries.AddRange(
            refundIssuedEntry,
            sellerRefundReversalEntry,
            platformRefundReversalEntry,
            providerFeeRefundReversalEntry);

        var balance = await dbContext.SellerBalances.SingleOrDefaultAsync(
            existing => existing.SellerId == order.SellerId && existing.Currency == refund.Currency,
            cancellationToken);
        if (balance is null)
        {
            balance = new SellerBalance(order.SellerId, refund.Currency);
            dbContext.SellerBalances.Add(balance);
        }

        if (sellerDebit > 0)
        {
            var unappliedDebit = await AdjustPayoutsForRefundAsync(
                refund,
                payment,
                order,
                sellerRefundReversalEntry.Id,
                balance,
                sellerDebit,
                createdAtUtc,
                cancellationToken);

            if (unappliedDebit > 0)
            {
                balance.ApplyRefundDebit(unappliedDebit);
            }
        }
    }

    private async Task<decimal> AdjustPayoutsForRefundAsync(
        Refund refund,
        Payment payment,
        Order order,
        Guid sellerRefundReversalEntryId,
        SellerBalance balance,
        decimal sellerDebit,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var remaining = sellerDebit;
        var adjustableStatuses = new[]
        {
            SellerPayoutStatus.Pending,
            SellerPayoutStatus.OnHold,
            SellerPayoutStatus.Available
        };

        var payouts = await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .Where(payout => payout.SellerId == order.SellerId
                && payout.Currency == refund.Currency
                && adjustableStatuses.Contains(payout.Status)
                && payout.Items.Any(item => item.PaymentId == payment.Id || item.OrderId == order.Id))
            .OrderBy(payout => payout.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var payout in payouts)
        {
            if (remaining <= 0)
            {
                break;
            }

            foreach (var item in payout.Items
                .Where(item => (item.PaymentId == payment.Id || item.OrderId == order.Id) && item.NetAmount > 0)
                .OrderBy(item => item.CreatedAtUtc))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var adjustmentAmount = RoundMoney(Math.Min(item.NetAmount, remaining));
                if (adjustmentAmount <= 0)
                {
                    continue;
                }

                var payoutStatus = payout.Status;
                item.ApplyAdjustment(adjustmentAmount);
                payout.ApplyAdjustment(adjustmentAmount, createdAtUtc);
                DebitBalanceForPayoutStatus(balance, payoutStatus, adjustmentAmount);

                dbContext.SellerPayoutAdjustments.Add(new SellerPayoutAdjustment(
                    payout.Id,
                    item.Id,
                    refund.Id,
                    sellerRefundReversalEntryId,
                    adjustmentAmount,
                    refund.Currency,
                    "RefundPayoutReduction",
                    createdAtUtc,
                    $"Refund {refund.Id} reduced unpaid payout item {item.Id}."));

                remaining = RoundMoney(remaining - adjustmentAmount);
            }
        }

        if (remaining <= 0)
        {
            return 0m;
        }

        var terminalPayout = await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .Where(payout => payout.SellerId == order.SellerId
                && payout.Currency == refund.Currency
                && (payout.Status == SellerPayoutStatus.Processing || payout.Status == SellerPayoutStatus.PaidOut)
                && payout.Items.Any(item => item.PaymentId == payment.Id || item.OrderId == order.Id))
            .OrderByDescending(payout => payout.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (terminalPayout is not null)
        {
            dbContext.SellerPayoutAdjustments.Add(new SellerPayoutAdjustment(
                terminalPayout.Id,
                null,
                refund.Id,
                sellerRefundReversalEntryId,
                remaining,
                refund.Currency,
                "RecoveryRequired",
                createdAtUtc,
                $"Refund {refund.Id} occurred after payout {terminalPayout.Id} reached {terminalPayout.Status}."));
        }

        return remaining;
    }

    private static void DebitBalanceForPayoutStatus(
        SellerBalance balance,
        SellerPayoutStatus payoutStatus,
        decimal amount)
    {
        if (payoutStatus == SellerPayoutStatus.Available)
        {
            balance.DebitAvailable(amount);
        }
        else if (payoutStatus == SellerPayoutStatus.OnHold)
        {
            balance.DebitHeld(amount);
        }
        else
        {
            balance.DebitPending(amount);
        }
    }

    private async Task<decimal> TotalRefundedOrPendingAsync(
        Guid paymentId,
        Guid? excludeRefundId,
        CancellationToken cancellationToken) =>
        await dbContext.Refunds
            .Where(refund => refund.PaymentId == paymentId
                && refund.Id != excludeRefundId
                && refund.Status != RefundStatus.Failed
                && refund.Status != RefundStatus.Rejected)
            .SumAsync(refund => refund.Amount, cancellationToken);

    private void TrackLatestRefundEvent(Refund refund)
    {
        var refundEvent = refund.Events.LastOrDefault();
        if (refundEvent is not null)
        {
            dbContext.RefundEvents.Add(refundEvent);
        }
    }

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private static List<ValidationFailure> ValidateCreateRequest(CreateRefundWorkflowRequest request)
    {
        var failures = new List<ValidationFailure>();
        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (request.ReturnRequestId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("returnRequestId", "Return request id cannot be empty."));
        }

        if (request.Amount <= 0)
        {
            failures.Add(new ValidationFailure("amount", "Refund amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            failures.Add(new ValidationFailure("reason", "Refund reason is required."));
        }

        if (request.ActorUserId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("actorUserId", "Actor user id is required."));
        }

        return failures;
    }

    private static List<ValidationFailure> ValidateManualProviderConfirmationRequest(
        ConfirmManualProviderRefundWorkflowRequest request)
    {
        var failures = new List<ValidationFailure>();
        if (request.RefundId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("refundId", "Refund id is required."));
        }

        if (request.ActorUserId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("actorUserId", "Actor user id is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ProviderRefundReference))
        {
            failures.Add(new ValidationFailure("providerRefundReference", "Provider refund reference is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            failures.Add(new ValidationFailure("reason", "Manual refund confirmation reason is required."));
        }

        return failures;
    }

    private static Result<RefundResult> Validation(string propertyName, string message) =>
        Result<RefundResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

    public static RefundResult Map(Refund refund) =>
        new(
            refund.Id,
            refund.OrderId,
            refund.PaymentId,
            refund.BuyerId,
            refund.SellerId,
            refund.ReturnRequestId,
            refund.Amount,
            refund.Currency,
            refund.Status.ToString(),
            refund.Reason,
            refund.ProviderRefundReference,
            refund.FailureReason,
            refund.RequestedAtUtc,
            refund.ApprovedAtUtc,
            refund.RefundedAtUtc,
            refund.Events
                .OrderBy(refundEvent => refundEvent.CreatedAtUtc)
                .Select(refundEvent => new RefundEventResult(
                    refundEvent.Id,
                    refundEvent.Status.ToString(),
                    refundEvent.EventType,
                    refundEvent.Message,
                    refundEvent.CreatedAtUtc))
                .ToArray());

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
