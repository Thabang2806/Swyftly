using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Results;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Ledger;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Payouts;

public static class PayoutEndpoints
{
    public static IEndpointRouteBuilder MapPayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var sellerGroup = app.MapGroup("/api/seller")
            .WithTags("Seller Payouts")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        sellerGroup.MapGet("/balance", GetSellerBalanceAsync)
            .WithName("GetSellerBalance")
            .WithSummary("Returns pending, available, and held balances for the authenticated seller.")
            .Produces<SellerBalanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("/payouts", GetSellerPayoutsAsync)
            .WithName("GetSellerPayouts")
            .WithSummary("Returns payout records for the authenticated seller.")
            .Produces<IReadOnlyCollection<SellerPayoutResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var adminGroup = app.MapGroup("/api/admin/payouts")
            .WithTags("Admin Payouts")
            .RequireAuthorization(MabuntlePolicies.FinanceRead);

        adminGroup.MapGet("/pending", GetAdminPendingPayoutsAsync)
            .WithName("GetAdminPendingPayouts")
            .WithSummary("Returns pending and held seller payouts for admin review.")
            .Produces<IReadOnlyCollection<AdminPayoutResponse>>(StatusCodes.Status200OK);

        adminGroup.MapPost("/{id:guid}/hold", HoldPayoutAsync)
            .WithName("HoldSellerPayout")
            .WithSummary("Places a seller payout on hold and writes an audit log.")
            .RequireAuthorization(MabuntlePolicies.FinanceOperate)
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{id:guid}/release", ReleasePayoutAsync)
            .WithName("ReleaseSellerPayout")
            .WithSummary("Releases a held seller payout back to pending and writes an audit log.")
            .RequireAuthorization(MabuntlePolicies.FinanceApprove)
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{id:guid}/make-available", MakeAvailableAsync)
            .WithName("MakeSellerPayoutAvailable")
            .WithSummary("Moves a pending seller payout into the available balance.")
            .RequireAuthorization(MabuntlePolicies.FinanceOperate)
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{id:guid}/process", ProcessPayoutAsync)
            .WithName("ProcessSellerPayout")
            .WithSummary("Starts provider payout processing for an available seller payout.")
            .RequireAuthorization(MabuntlePolicies.FinanceApprove)
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapPost("/{id:guid}/reconcile", ReconcilePayoutAsync)
            .WithName("ReconcileSellerPayout")
            .WithSummary("Reconciles a processing seller payout with the payout provider.")
            .RequireAuthorization(MabuntlePolicies.FinanceApprove)
            .Produces<SellerPayoutResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetSellerBalanceAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var balances = await dbContext.SellerBalances
            .AsNoTracking()
            .Where(balance => balance.SellerId == seller.Id)
            .OrderBy(balance => balance.Currency)
            .Select(balance => new SellerCurrencyBalanceResponse(
                balance.Currency,
                balance.PendingBalance,
                balance.AvailableBalance,
                balance.HeldBalance))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(new SellerBalanceResponse(seller.Id, balances));
    }

    private static async Task<IResult> GetSellerPayoutsAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var payouts = await QueryPayouts(dbContext)
            .Where(payout => payout.SellerId == seller.Id)
            .OrderByDescending(payout => payout.CreatedAtUtc)
            .Select(payout => MapSellerPayout(payout))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(payouts);
    }

    private static async Task<IResult> GetAdminPendingPayoutsAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var payouts = await QueryPayouts(dbContext)
            .Where(payout => payout.Status == SellerPayoutStatus.Pending || payout.Status == SellerPayoutStatus.OnHold)
            .OrderBy(payout => payout.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var sellerIds = payouts.Select(payout => payout.SellerId).Distinct().ToArray();
        var pendingChangeRequests = await dbContext.SellerPayoutProfileChangeRequests
            .AsNoTracking()
            .Where(request => sellerIds.Contains(request.SellerId)
                && request.Status == SellerPayoutProfileChangeRequestStatus.PendingReview)
            .ToDictionaryAsync(request => request.SellerId, request => request.Id, cancellationToken);

        var responses = payouts
            .Select(payout => MapAdminPayout(
                payout,
                pendingChangeRequests.TryGetValue(payout.SellerId, out var requestId) ? requestId : null))
            .ToList();

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> HoldPayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return HttpResults.Problem(
                title: "Payouts.ActorNotFound",
                detail: "The authenticated admin user id could not be read.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await payoutAdministrationService.HoldAsync(
            new PayoutHoldRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> MakeAvailableAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await payoutAdministrationService.MakeAvailableAsync(
            new PayoutMakeAvailableRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ReleasePayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return HttpResults.Problem(
                title: "Payouts.ActorNotFound",
                detail: "The authenticated admin user id could not be read.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await payoutAdministrationService.ReleaseAsync(
            new PayoutReleaseRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ProcessPayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await payoutAdministrationService.ProcessAsync(
            new PayoutProcessRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ReconcilePayoutAsync(
        Guid id,
        PayoutReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IPayoutAdministrationService payoutAdministrationService,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var result = await payoutAdministrationService.ReconcileAsync(
            new PayoutReconcileRequest(
                id,
                actorUserId.Value,
                GetActorRole(principal),
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static IQueryable<SellerPayout> QueryPayouts(MabuntleDbContext dbContext) =>
        dbContext.SellerPayouts
            .Include(payout => payout.Items)
            .AsNoTracking();

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static SellerPayoutResponse MapSellerPayout(SellerPayout payout) =>
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
            payout.Items
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new SellerPayoutItemResponse(
                    item.Amount,
                    item.Currency,
                    item.CreatedAtUtc,
                    item.OrderId.HasValue ? "Order" : "Ledger"))
                .ToArray());

    private static AdminPayoutResponse MapAdminPayout(SellerPayout payout, Guid? pendingPayoutProfileChangeRequestId = null) =>
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
            pendingPayoutProfileChangeRequestId.HasValue,
            pendingPayoutProfileChangeRequestId,
            payout.Items
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new AdminPayoutItemResponse(
                    item.Id,
                    item.LedgerEntryId,
                    item.OrderId,
                    item.PaymentId,
                    item.Amount,
                    item.Currency,
                    item.CreatedAtUtc))
                .ToArray());

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : principal.IsInRole(MabuntleRoles.FinanceApprover)
                ? MabuntleRoles.FinanceApprover
                : principal.IsInRole(MabuntleRoles.FinanceOperator)
                    ? MabuntleRoles.FinanceOperator
                    : MabuntleRoles.Admin;

    private static Guid? GetActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "Payouts.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "Payouts.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record PayoutReasonRequest(string Reason);

public sealed record SellerBalanceResponse(
    Guid SellerId,
    IReadOnlyCollection<SellerCurrencyBalanceResponse> Balances);

public sealed record SellerCurrencyBalanceResponse(
    string Currency,
    decimal PendingBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record SellerPayoutResponse(
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
    IReadOnlyCollection<SellerPayoutItemResponse> Items);

public sealed record SellerPayoutItemResponse(
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    string SourceType);

public sealed record AdminPayoutResponse(
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
    bool HasPendingPayoutProfileChange,
    Guid? PendingPayoutProfileChangeRequestId,
    IReadOnlyCollection<AdminPayoutItemResponse> Items);

public sealed record AdminPayoutItemResponse(
    Guid PayoutItemId,
    Guid LedgerEntryId,
    Guid? OrderId,
    Guid? PaymentId,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc);
