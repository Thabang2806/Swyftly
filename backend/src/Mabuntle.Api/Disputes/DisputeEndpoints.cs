using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Results;
using Mabuntle.Application.Disputes;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Disputes;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Disputes;

public static class DisputeEndpoints
{
    public static IEndpointRouteBuilder MapDisputeEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/buyer")
            .WithTags("Buyer Disputes")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly);

        buyerGroup.MapPost("/orders/{orderId:guid}/disputes", OpenOrderDisputeAsync)
            .WithName("OpenBuyerOrderDispute")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapPost("/returns/{returnRequestId:guid}/disputes", OpenReturnDisputeAsync)
            .WithName("OpenBuyerReturnDispute")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapGet("/disputes", GetBuyerDisputesAsync)
            .WithName("GetBuyerDisputes")
            .Produces<IReadOnlyCollection<DisputeResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapPost("/disputes/{disputeId:guid}/messages", AddBuyerMessageAsync)
            .WithName("AddBuyerDisputeMessage")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapPost("/disputes/{disputeId:guid}/evidence", AddBuyerEvidenceAsync)
            .WithName("AddBuyerDisputeEvidence")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sellerGroup = app.MapGroup("/api/seller/disputes")
            .WithTags("Seller Disputes")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        sellerGroup.MapGet("", GetSellerDisputesAsync)
            .WithName("GetSellerDisputes")
            .Produces<IReadOnlyCollection<DisputeResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{disputeId:guid}/messages", AddSellerMessageAsync)
            .WithName("AddSellerDisputeMessage")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/{disputeId:guid}/evidence", AddSellerEvidenceAsync)
            .WithName("AddSellerDisputeEvidence")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var adminGroup = app.MapGroup("/api/admin/disputes")
            .WithTags("Admin Disputes")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        adminGroup.MapGet("", GetAdminDisputesAsync)
            .WithName("GetAdminDisputes")
            .Produces<IReadOnlyCollection<DisputeResult>>(StatusCodes.Status200OK);

        adminGroup.MapPost("/{disputeId:guid}/resolve", ResolveDisputeAsync)
            .WithName("ResolveDispute")
            .Produces<DisputeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> OpenOrderDisputeAsync(
        Guid orderId,
        OpenDisputeApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var result = await disputeWorkflowService.OpenDisputeAsync(
            new OpenDisputeRequest(
                buyer.Id,
                buyerUserId,
                orderId,
                null,
                request.Reason,
                MapEvidence(request.Evidence),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> OpenReturnDisputeAsync(
        Guid returnRequestId,
        OpenDisputeApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var orderId = await dbContext.ReturnRequests
            .Where(returnRequest => returnRequest.Id == returnRequestId && returnRequest.BuyerId == buyer.Id)
            .Select(returnRequest => returnRequest.OrderId)
            .SingleOrDefaultAsync(cancellationToken);
        if (orderId == Guid.Empty)
        {
            return HttpResults.Problem(
                title: "Disputes.ReturnNotFound",
                detail: "Return request was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var result = await disputeWorkflowService.OpenDisputeAsync(
            new OpenDisputeRequest(
                buyer.Id,
                buyerUserId,
                orderId,
                returnRequestId,
                request.Reason,
                MapEvidence(request.Evidence),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetBuyerDisputesAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var disputes = await DisputeQuery(dbContext)
            .Where(dispute => dispute.BuyerId == buyer.Id)
            .OrderByDescending(dispute => dispute.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(disputes.Select(EfDisputeWorkflowService.Map).ToArray());
    }

    private static async Task<IResult> GetSellerDisputesAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var disputes = await DisputeQuery(dbContext)
            .Where(dispute => dispute.SellerId == seller.Id)
            .OrderByDescending(dispute => dispute.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(disputes.Select(EfDisputeWorkflowService.Map).ToArray());
    }

    private static async Task<IResult> GetAdminDisputesAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var disputes = await DisputeQuery(dbContext)
            .OrderByDescending(dispute => dispute.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(disputes.Select(EfDisputeWorkflowService.Map).ToArray());
    }

    private static async Task<IResult> AddBuyerMessageAsync(
        Guid disputeId,
        DisputeMessageApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        return await AddMessageAsync(disputeId, request, principal, buyer.Id, MabuntleRoles.Buyer, disputeWorkflowService, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddSellerMessageAsync(
        Guid disputeId,
        DisputeMessageApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        return await AddMessageAsync(disputeId, request, principal, seller.Id, MabuntleRoles.Seller, disputeWorkflowService, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddBuyerEvidenceAsync(
        Guid disputeId,
        DisputeEvidenceApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        return await AddEvidenceAsync(disputeId, request, principal, buyer.Id, MabuntleRoles.Buyer, disputeWorkflowService, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddSellerEvidenceAsync(
        Guid disputeId,
        DisputeEvidenceApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        return await AddEvidenceAsync(disputeId, request, principal, seller.Id, MabuntleRoles.Seller, disputeWorkflowService, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddMessageAsync(
        Guid disputeId,
        DisputeMessageApiRequest request,
        ClaimsPrincipal principal,
        Guid actorProfileId,
        string actorRole,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var result = await disputeWorkflowService.AddMessageAsync(
            new AddDisputeMessageRequest(
                disputeId,
                actorProfileId,
                actorUserId,
                actorRole,
                request.Message,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> AddEvidenceAsync(
        Guid disputeId,
        DisputeEvidenceApiRequest request,
        ClaimsPrincipal principal,
        Guid actorProfileId,
        string actorRole,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var result = await disputeWorkflowService.AddEvidenceAsync(
            new AddDisputeEvidenceRequest(
                disputeId,
                actorProfileId,
                actorUserId,
                actorRole,
                new DisputeEvidenceInput(request.EvidenceType, request.StorageReference, request.Description),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ResolveDisputeAsync(
        Guid disputeId,
        ResolveDisputeApiRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IDisputeWorkflowService disputeWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var result = await disputeWorkflowService.ResolveAsync(
            new ResolveDisputeRequest(
                disputeId,
                actorUserId,
                GetActorRole(principal),
                request.Outcome,
                request.Reason,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static IQueryable<Dispute> DisputeQuery(MabuntleDbContext dbContext) =>
        dbContext.Disputes
            .Include(dispute => dispute.Messages)
            .Include(dispute => dispute.Evidence)
            .AsNoTracking();

    private static IReadOnlyCollection<DisputeEvidenceInput> MapEvidence(IReadOnlyCollection<DisputeEvidenceApiRequest>? evidence) =>
        evidence?.Select(item => new DisputeEvidenceInput(item.EvidenceType, item.StorageReference, item.Description)).ToArray()
        ?? [];

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

    private static IResult BuyerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a buyer profile.", statusCode: StatusCodes.Status404NotFound, title: "Disputes.BuyerNotFound");

    private static IResult SellerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a seller profile.", statusCode: StatusCodes.Status404NotFound, title: "Disputes.SellerNotFound");

    private static IResult UserNotFound() =>
        HttpResults.Problem("The authenticated user id could not be resolved.", statusCode: StatusCodes.Status404NotFound, title: "Disputes.UserNotFound");
}

public sealed record OpenDisputeApiRequest(
    string Reason,
    IReadOnlyCollection<DisputeEvidenceApiRequest>? Evidence);

public sealed record DisputeMessageApiRequest(string Message);

public sealed record DisputeEvidenceApiRequest(
    string EvidenceType,
    string StorageReference,
    string? Description);

public sealed record ResolveDisputeApiRequest(
    string Outcome,
    string Reason);
