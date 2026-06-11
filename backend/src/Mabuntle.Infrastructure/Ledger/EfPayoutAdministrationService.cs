using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Ledger;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Ledger;

public sealed class EfPayoutAdministrationService(
    MabuntleDbContext dbContext,
    IPayoutProvider payoutProvider,
    IAuditLogService auditLogService,
    TimeProvider timeProvider) : IPayoutAdministrationService
{
    public async Task<Result<SellerPayoutResult>> MakeAvailableAsync(
        PayoutMakeAvailableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return NotFound();
        }

        if (payout.Status == SellerPayoutStatus.Available)
        {
            return Result<SellerPayoutResult>.Success(Map(payout));
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.MakeAvailable(request.ActorUserId.ToString(), request.Reason, now);
            balance.MovePendingToAvailable(payout.Amount);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutMadeAvailable",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        return await SaveAndMapAsync(payout, cancellationToken);
    }

    public async Task<Result<SellerPayoutResult>> HoldAsync(
        PayoutHoldRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return NotFound();
        }

        if (payout.Status == SellerPayoutStatus.OnHold)
        {
            return Result<SellerPayoutResult>.Success(Map(payout));
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.Hold(request.ActorUserId.ToString(), request.Reason, now);
            if (previousStatus == SellerPayoutStatus.Pending)
            {
                balance.HoldPending(payout.Amount);
            }
            else
            {
                balance.HoldAvailable(payout.Amount);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutHeld",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        return await SaveAndMapAsync(payout, cancellationToken);
    }

    public async Task<Result<SellerPayoutResult>> ReleaseAsync(
        PayoutReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return NotFound();
        }

        if (string.Equals(payout.HeldByUserId, request.ActorUserId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return DualControlFailure("The user who held a payout cannot release the same payout.");
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var heldFromStatus = payout.HeldFromStatus ?? SellerPayoutStatus.Pending;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.Release(request.ActorUserId.ToString(), request.Reason, now);
            if (heldFromStatus == SellerPayoutStatus.Available)
            {
                balance.ReleaseHeldToAvailable(payout.Amount);
            }
            else
            {
                balance.ReleaseHeldToPending(payout.Amount);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutReleased",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        return await SaveAndMapAsync(payout, cancellationToken);
    }

    public async Task<Result<SellerPayoutResult>> ProcessAsync(
        PayoutProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return NotFound();
        }

        if (payout.Status == SellerPayoutStatus.PaidOut)
        {
            return Result<SellerPayoutResult>.Success(Map(payout));
        }

        if (payout.Status == SellerPayoutStatus.Processing)
        {
            return Result<SellerPayoutResult>.Failure(Error.Conflict(
                "Payouts.AlreadyProcessing",
                "Payout processing has already started. Reconcile the provider state before retrying."));
        }

        if (string.Equals(payout.AvailableByUserId, request.ActorUserId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return DualControlFailure("The user who made a payout available cannot process the same payout.");
        }

        var hasPendingPayoutProfileChange = await dbContext.SellerPayoutProfileChangeRequests.AnyAsync(
            changeRequest => changeRequest.SellerId == payout.SellerId
                && changeRequest.Status == SellerPayoutProfileChangeRequestStatus.PendingReview,
            cancellationToken);
        if (hasPendingPayoutProfileChange)
        {
            return Result<SellerPayoutResult>.Failure(Error.Conflict(
                "Payouts.PayoutProfileChangePending",
                "Payout processing is blocked while the seller has a pending payout profile change request."));
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        var previousStatus = payout.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            payout.StartProcessing(request.ActorUserId.ToString(), request.Reason, now);
            balance.DebitAvailable(payout.Amount);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("payout", exception.Message);
        }

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutProcessingStarted",
            payout,
            previousStatus,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        var saveResult = await SaveAndMapAsync(payout, cancellationToken);
        if (saveResult.IsFailure)
        {
            return saveResult;
        }

        var providerResult = await payoutProvider.InitiatePayoutAsync(
            new PayoutProviderRequest(
                payout.Id,
                payout.SellerId,
                payout.Amount,
                payout.Currency,
                payout.Id.ToString("N"),
                new Dictionary<string, string>
                {
                    ["payoutId"] = payout.Id.ToString(),
                    ["sellerId"] = payout.SellerId.ToString()
                }),
            cancellationToken);

        if (providerResult.IsFailure)
        {
            payout.MarkFailed(
                payoutProvider.ProviderName,
                $"provider_error_{payout.Id:N}",
                FakePayoutProviderOutcomes.Failed,
                providerResult.Error.Description,
                timeProvider.GetUtcNow());
            balance.CreditAvailable(payout.Amount);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<SellerPayoutResult>.Failure(providerResult.Error);
        }

        ApplyProviderResult(payout, balance, providerResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SellerPayoutResult>.Success(Map(payout));
    }

    public async Task<Result<SellerPayoutResult>> ReconcileAsync(
        PayoutReconcileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Validation("reason", "Reason is required.");
        }

        var payout = await GetPayoutAsync(request.PayoutId, cancellationToken);
        if (payout is null)
        {
            return NotFound();
        }

        if (payout.Status == SellerPayoutStatus.PaidOut)
        {
            return Result<SellerPayoutResult>.Success(Map(payout));
        }

        if (payout.Status != SellerPayoutStatus.Processing)
        {
            return Validation("payout", "Only processing payouts can be reconciled.");
        }

        if (string.Equals(payout.ProcessingByUserId, request.ActorUserId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return DualControlFailure("The user who started payout processing cannot reconcile the same payout.");
        }

        if (string.IsNullOrWhiteSpace(payout.ProviderPayoutReference))
        {
            return Validation("payout", "Payout provider reference is missing.");
        }

        var providerResult = await payoutProvider.GetPayoutAsync(
            new PayoutProviderStatusRequest(payout.ProviderPayoutReference),
            cancellationToken);
        if (providerResult.IsFailure)
        {
            return Result<SellerPayoutResult>.Failure(providerResult.Error);
        }

        var balance = await GetRequiredBalanceAsync(payout, cancellationToken);
        ApplyProviderResult(payout, balance, providerResult.Value);

        await RecordAuditAsync(
            request.ActorUserId,
            request.ActorRole,
            "PayoutReconciled",
            payout,
            SellerPayoutStatus.Processing,
            payout.Status,
            request.Reason,
            request.IpAddress,
            cancellationToken);

        return await SaveAndMapAsync(payout, cancellationToken);
    }

    private static void ApplyProviderResult(
        SellerPayout payout,
        SellerBalance balance,
        PayoutProviderResult providerResult)
    {
        if (string.Equals(providerResult.Status, FakePayoutProviderOutcomes.PaidOut, StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            payout.MarkPaidOut(
                providerResult.Provider,
                providerResult.ProviderPayoutReference,
                providerResult.Status,
                providerResult.ProcessedAtUtc);
            return;
        }

        if (string.Equals(providerResult.Status, FakePayoutProviderOutcomes.Failed, StringComparison.OrdinalIgnoreCase))
        {
            payout.MarkFailed(
                providerResult.Provider,
                providerResult.ProviderPayoutReference,
                providerResult.Status,
                providerResult.FailureReason ?? "Payout provider marked the payout failed.",
                providerResult.ProcessedAtUtc);
            balance.CreditAvailable(payout.Amount);
            return;
        }

        payout.RecordProviderProcessing(
            providerResult.Provider,
            providerResult.ProviderPayoutReference,
            providerResult.Status,
            providerResult.ProcessedAtUtc);
    }

    private async Task<SellerPayout?> GetPayoutAsync(Guid payoutId, CancellationToken cancellationToken) =>
        await dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .SingleOrDefaultAsync(payout => payout.Id == payoutId, cancellationToken);

    private async Task<SellerBalance> GetRequiredBalanceAsync(SellerPayout payout, CancellationToken cancellationToken) =>
        await dbContext.SellerBalances.SingleAsync(
            balance => balance.SellerId == payout.SellerId && balance.Currency == payout.Currency,
            cancellationToken);

    private async Task RecordAuditAsync(
        Guid actorUserId,
        string actorRole,
        string actionType,
        SellerPayout payout,
        SellerPayoutStatus previousStatus,
        SellerPayoutStatus newStatus,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId.ToString(),
                actorRole,
                actionType,
                "SellerPayout",
                payout.Id.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { status = newStatus.ToString(), amount = payout.Amount }),
                reason,
                ipAddress),
            cancellationToken);
    }

    private async Task<Result<SellerPayoutResult>> SaveAndMapAsync(
        SellerPayout payout,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SellerPayoutResult>.Failure(Error.Conflict(
                "Payouts.ConcurrentUpdate",
                "Payout state was already changed by another process."));
        }

        return Result<SellerPayoutResult>.Success(Map(payout));
    }

    private static Result<SellerPayoutResult> NotFound() =>
        Result<SellerPayoutResult>.Failure(Error.NotFound("Payouts.NotFound", "Seller payout was not found."));

    private static Result<SellerPayoutResult> DualControlFailure(string description) =>
        Result<SellerPayoutResult>.Failure(Error.Forbidden("Payouts.DualControlRequired", description));

    private static SellerPayoutResult Map(SellerPayout payout) =>
        new(
            payout.Id,
            payout.SellerId,
            payout.Amount,
            payout.Currency,
            payout.Status.ToString(),
            payout.CreatedAtUtc,
            payout.HeldAtUtc,
            payout.HoldReason,
            payout.ReleasedAtUtc,
            payout.ReleaseReason,
            payout.AvailableAtUtc,
            payout.ProcessingAtUtc,
            payout.PaidOutAtUtc,
            payout.FailedAtUtc,
            payout.FailureReason,
            payout.ProviderName,
            payout.ProviderPayoutReference);

    private static Result<SellerPayoutResult> Validation(string propertyName, string message) =>
        Result<SellerPayoutResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));
}
