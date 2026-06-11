using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Disputes;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Ledger;
using Mabuntle.Application.Refunds;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Infrastructure.Persistence;
using Mabuntle.Infrastructure.Refunds;

namespace Mabuntle.Infrastructure.Disputes;

public sealed class EfDisputeWorkflowService(
    MabuntleDbContext dbContext,
    IPayoutAdministrationService payoutAdministrationService,
    IAuditLogService auditLogService) : IDisputeWorkflowService
{
    public async Task<Result<DisputeResult>> OpenDisputeAsync(
        OpenDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateOpen(request);
        if (validation.Count > 0)
        {
            return Result<DisputeResult>.Failure(Error.Validation(validation));
        }

        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleOrDefaultAsync(
                existing => existing.Id == request.OrderId && existing.BuyerId == request.BuyerId,
                cancellationToken);
        if (order is null)
        {
            return Result<DisputeResult>.Failure(Error.NotFound("Disputes.OrderNotFound", "Order was not found."));
        }

        if (order.Status is not (OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Disputed))
        {
            return Result<DisputeResult>.Failure(
                Error.Conflict("Disputes.OrderNotEligible", "Only delivered, return-requested, or disputed orders can have disputes."));
        }

        ReturnRequest? returnRequest = null;
        if (request.ReturnRequestId.HasValue)
        {
            returnRequest = await dbContext.ReturnRequests.SingleOrDefaultAsync(
                existing => existing.Id == request.ReturnRequestId.Value
                    && existing.OrderId == order.Id
                    && existing.BuyerId == request.BuyerId,
                cancellationToken);
            if (returnRequest is null)
            {
                return Result<DisputeResult>.Failure(Error.NotFound("Disputes.ReturnNotFound", "Return request was not found."));
            }
        }

        var hasActiveDispute = await dbContext.Disputes.AnyAsync(
            dispute => dispute.OrderId == order.Id
                && dispute.Status != DisputeStatus.ResolvedBuyerFavoured
                && dispute.Status != DisputeStatus.ResolvedSellerFavoured
                && dispute.Status != DisputeStatus.Closed,
            cancellationToken);
        if (hasActiveDispute)
        {
            return Result<DisputeResult>.Failure(
                Error.Conflict("Disputes.ActiveDisputeExists", "An active dispute already exists for this order."));
        }

        var dispute = new Dispute(
            order.Id,
            request.ReturnRequestId,
            order.BuyerId,
            order.SellerId,
            request.BuyerUserId,
            request.Reason,
            request.OpenedAtUtc);

        foreach (var evidence in request.Evidence)
        {
            try
            {
                dispute.AddEvidence(
                    request.BuyerUserId,
                    MabuntleRoles.Buyer,
                    evidence.EvidenceType,
                    evidence.StorageReference,
                    evidence.Description,
                    request.OpenedAtUtc);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Validation("evidence", exception.Message);
            }
        }

        order.ChangeStatus(OrderStatus.Disputed, request.OpenedAtUtc, "DisputeOpened");
        TrackLatestOrderStatusHistory(order);
        if (returnRequest is not null && returnRequest.Status == ReturnStatus.Rejected)
        {
            try
            {
                returnRequest.Dispute(request.BuyerUserId, request.Reason, request.OpenedAtUtc);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Validation("returnRequest", exception.Message);
            }
        }

        dbContext.Disputes.Add(dispute);
        var holdResult = await HoldLinkedPayoutsAsync(order.Id, order.SellerId, request.BuyerUserId, dispute.Id, cancellationToken);
        if (holdResult.IsFailure)
        {
            return Result<DisputeResult>.Failure(holdResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DisputeResult>.Success(Map(dispute));
    }

    public async Task<Result<DisputeResult>> AddMessageAsync(
        AddDisputeMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Validation("message", "Message is required.");
        }

        var dispute = await GetActorDisputeAsync(request.DisputeId, request.ActorProfileId, request.ActorRole, cancellationToken);
        if (dispute is null)
        {
            return DisputeNotFound();
        }

        try
        {
            dispute.AddMessage(request.ActorUserId, request.ActorRole, request.Message, request.CreatedAtUtc);
            TrackLatestMessage(dispute);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("dispute", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DisputeResult>.Success(Map(dispute));
    }

    public async Task<Result<DisputeResult>> AddEvidenceAsync(
        AddDisputeEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var dispute = await GetActorDisputeAsync(request.DisputeId, request.ActorProfileId, request.ActorRole, cancellationToken);
        if (dispute is null)
        {
            return DisputeNotFound();
        }

        try
        {
            dispute.AddEvidence(
                request.ActorUserId,
                request.ActorRole,
                request.Evidence.EvidenceType,
                request.Evidence.StorageReference,
                request.Evidence.Description,
                request.CreatedAtUtc);
            TrackLatestEvidence(dispute);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("evidence", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DisputeResult>.Success(Map(dispute));
    }

    public async Task<Result<DisputeResult>> ResolveAsync(
        ResolveDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Resolution reason is required.");
        }

        var dispute = await dbContext.Disputes
            .Include(existing => existing.Messages)
            .Include(existing => existing.Evidence)
            .SingleOrDefaultAsync(existing => existing.Id == request.DisputeId, cancellationToken);
        if (dispute is null)
        {
            return DisputeNotFound();
        }

        var previousStatus = dispute.Status;
        try
        {
            dispute.MarkUnderAdminReview(request.ResolvedAtUtc);
            if (string.Equals(request.Outcome, "BuyerFavoured", StringComparison.OrdinalIgnoreCase))
            {
                dispute.ResolveBuyerFavoured(request.ActorUserId, request.Reason, request.ResolvedAtUtc);
            }
            else if (string.Equals(request.Outcome, "SellerFavoured", StringComparison.OrdinalIgnoreCase))
            {
                dispute.ResolveSellerFavoured(request.ActorUserId, request.Reason, request.ResolvedAtUtc);
            }
            else
            {
                return Validation("outcome", "Outcome must be BuyerFavoured or SellerFavoured.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("dispute", exception.Message);
        }

        RefundResult? buyerFavouredRefund = null;
        if (dispute.Status == DisputeStatus.ResolvedSellerFavoured)
        {
            await ReleaseLinkedPayoutsAsync(dispute.OrderId, dispute.SellerId, request, cancellationToken);
        }
        else if (dispute.Status == DisputeStatus.ResolvedBuyerFavoured)
        {
            var refundResult = await CreateBuyerFavouredRefundRequestAsync(dispute, request, cancellationToken);
            if (refundResult.IsFailure)
            {
                return Result<DisputeResult>.Failure(refundResult.Error);
            }

            buyerFavouredRefund = refundResult.Value;
        }

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                request.ActorUserId.ToString(),
                request.ActorRole,
                "DisputeResolved",
                "Dispute",
                dispute.Id.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new
                {
                    status = dispute.Status.ToString(),
                    outcome = request.Outcome,
                    refundId = buyerFavouredRefund?.RefundId,
                    refundAmount = buyerFavouredRefund?.Amount
                }),
                request.Reason,
                request.IpAddress),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DisputeResult>.Success(Map(dispute));
    }

    private async Task<Result> HoldLinkedPayoutsAsync(
        Guid orderId,
        Guid sellerId,
        Guid actorUserId,
        Guid disputeId,
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
                    MabuntleRoles.Buyer,
                    $"Dispute {disputeId} is active.",
                    null),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }
        }

        return Result.Success();
    }

    private async Task ReleaseLinkedPayoutsAsync(
        Guid orderId,
        Guid sellerId,
        ResolveDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var payouts = await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .Where(payout => payout.SellerId == sellerId
                && payout.Items.Any(item => item.OrderId == orderId)
                && payout.Status == SellerPayoutStatus.OnHold)
            .ToListAsync(cancellationToken);

        foreach (var payout in payouts)
        {
            await payoutAdministrationService.ReleaseAsync(
                new PayoutReleaseRequest(
                    payout.Id,
                    request.ActorUserId,
                    request.ActorRole,
                    $"Seller-favoured dispute resolution: {request.Reason}",
                    request.IpAddress),
                cancellationToken);
        }
    }

    private async Task<Result<RefundResult>> CreateBuyerFavouredRefundRequestAsync(
        Dispute dispute,
        ResolveDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .Where(existing => existing.OrderId == dispute.OrderId
                && (existing.Status == PaymentStatus.Paid || existing.Status == PaymentStatus.PartiallyRefunded))
            .OrderByDescending(existing => existing.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            return Result<RefundResult>.Failure(Error.Conflict(
                "Disputes.RefundablePaymentNotFound",
                "Buyer-favoured disputes require a paid or partially refunded payment before refund recovery can start."));
        }

        var alreadyRefundedOrPending = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.PaymentId == payment.Id
                && refund.Status != RefundStatus.Failed
                && refund.Status != RefundStatus.Rejected)
            .SumAsync(refund => refund.Amount, cancellationToken);
        var remainingRefundableAmount = payment.Amount - alreadyRefundedOrPending;
        if (remainingRefundableAmount <= 0)
        {
            return Result<RefundResult>.Failure(Error.Conflict(
                "Disputes.NoRefundableBalance",
                "The disputed order has no remaining refundable payment balance."));
        }

        if (dispute.ReturnRequestId.HasValue)
        {
            var returnRequest = await dbContext.ReturnRequests.SingleOrDefaultAsync(
                existing => existing.Id == dispute.ReturnRequestId.Value && existing.OrderId == dispute.OrderId,
                cancellationToken);
            if (returnRequest is not null && returnRequest.Status != ReturnStatus.RefundPending)
            {
                try
                {
                    returnRequest.MarkRefundPending(request.ResolvedAtUtc);
                }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
                {
                    return Result<RefundResult>.Failure(Error.Conflict(
                        "Disputes.ReturnNotRefundable",
                        exception.Message));
                }
            }
        }

        var refund = new Refund(
            dispute.OrderId,
            payment.Id,
            dispute.BuyerId,
            dispute.SellerId,
            dispute.ReturnRequestId,
            remainingRefundableAmount,
            payment.Currency,
            $"Buyer-favoured dispute {dispute.Id}: {request.Reason}",
            request.ActorUserId,
            request.ActorRole,
            request.ResolvedAtUtc);

        dbContext.Refunds.Add(refund);
        return Result<RefundResult>.Success(EfRefundWorkflowService.Map(refund));
    }

    private async Task<Dispute?> GetActorDisputeAsync(
        Guid disputeId,
        Guid actorProfileId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Disputes
            .Include(dispute => dispute.Messages)
            .Include(dispute => dispute.Evidence)
            .Where(dispute => dispute.Id == disputeId);

        if (string.Equals(actorRole, MabuntleRoles.Buyer, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(dispute => dispute.BuyerId == actorProfileId);
        }
        else if (string.Equals(actorRole, MabuntleRoles.Seller, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(dispute => dispute.SellerId == actorProfileId);
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private void TrackLatestMessage(Dispute dispute)
    {
        var message = dispute.Messages.LastOrDefault();
        if (message is not null)
        {
            dbContext.DisputeMessages.Add(message);
        }
    }

    private void TrackLatestEvidence(Dispute dispute)
    {
        var evidence = dispute.Evidence.LastOrDefault();
        if (evidence is not null)
        {
            dbContext.DisputeEvidence.Add(evidence);
        }
    }

    private static List<ValidationFailure> ValidateOpen(OpenDisputeRequest request)
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

        if (request.ReturnRequestId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("returnRequestId", "Return request id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            failures.Add(new ValidationFailure("reason", "Dispute reason is required."));
        }

        return failures;
    }

    private static Result<DisputeResult> DisputeNotFound() =>
        Result<DisputeResult>.Failure(Error.NotFound("Disputes.NotFound", "Dispute was not found."));

    private static Result<DisputeResult> Validation(string propertyName, string message) =>
        Result<DisputeResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

    public static DisputeResult Map(Dispute dispute) =>
        new(
            dispute.Id,
            dispute.OrderId,
            dispute.ReturnRequestId,
            dispute.BuyerId,
            dispute.SellerId,
            dispute.Status.ToString(),
            dispute.Reason,
            dispute.OpenedAtUtc,
            dispute.ResolvedAtUtc,
            dispute.ResolutionReason,
            dispute.Messages
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => new DisputeMessageResult(
                    message.Id,
                    message.SenderUserId,
                    message.SenderRole,
                    message.Message,
                    message.CreatedAtUtc))
                .ToArray(),
            dispute.Evidence
                .OrderBy(evidence => evidence.CreatedAtUtc)
                .Select(evidence => new DisputeEvidenceResult(
                    evidence.Id,
                    evidence.SubmittedByUserId,
                    evidence.SubmittedByRole,
                    evidence.EvidenceType,
                    evidence.StorageReference,
                    evidence.Description,
                    evidence.CreatedAtUtc))
                .ToArray());
}
