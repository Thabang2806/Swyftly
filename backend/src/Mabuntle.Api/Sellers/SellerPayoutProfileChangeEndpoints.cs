using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerPayoutProfileChangeEndpoints
{
    public static IEndpointRouteBuilder MapSellerPayoutProfileChangeEndpoints(this IEndpointRouteBuilder app)
    {
        var sellerGroup = app.MapGroup("/api/seller/payout-profile")
            .WithTags("Seller Payout Profile")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        sellerGroup.MapGet("/change-request", GetSellerStateAsync)
            .WithName("GetSellerPayoutProfileChangeRequest")
            .WithSummary("Returns the seller payout profile change request state.")
            .Produces<SellerPayoutProfileChangeStateResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPut("/change-request", UpsertSellerDraftAsync)
            .WithName("UpsertSellerPayoutProfileChangeRequest")
            .WithSummary("Creates or updates a draft payout profile change request for a verified seller.")
            .Produces<SellerPayoutProfileChangeStateResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/change-request/submit-review", SubmitSellerDraftAsync)
            .WithName("SubmitSellerPayoutProfileChangeRequest")
            .WithSummary("Submits a draft payout profile change request for finance review.")
            .Produces<SellerPayoutProfileChangeStateResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/change-request/cancel", CancelSellerRequestAsync)
            .WithName("CancelSellerPayoutProfileChangeRequest")
            .WithSummary("Cancels a draft or pending payout profile change request.")
            .Produces<SellerPayoutProfileChangeStateResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var adminGroup = app.MapGroup("/api/admin/sellers/payout-profile-change-requests")
            .WithTags("Admin Seller Payout Profile Changes")
            .RequireAuthorization(MabuntlePolicies.FinanceRead);

        adminGroup.MapGet("", GetAdminQueueAsync)
            .WithName("GetAdminPayoutProfileChangeRequests")
            .WithSummary("Returns pending seller payout profile change requests for finance review.")
            .Produces<IReadOnlyCollection<AdminPayoutProfileChangeRequestResponse>>(StatusCodes.Status200OK);

        adminGroup.MapGet("/{requestId:guid}", GetAdminDetailAsync)
            .WithName("GetAdminPayoutProfileChangeRequest")
            .WithSummary("Returns a seller payout profile change request for finance review.")
            .Produces<AdminPayoutProfileChangeRequestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{requestId:guid}/approve", ApproveAsync)
            .WithName("ApproveSellerPayoutProfileChangeRequest")
            .WithSummary("Approves a seller payout profile change request and updates the live payout profile.")
            .RequireAuthorization(MabuntlePolicies.FinanceApprove)
            .Produces<AdminPayoutProfileChangeRequestResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapPost("/{requestId:guid}/reject", RejectAsync)
            .WithName("RejectSellerPayoutProfileChangeRequest")
            .WithSummary("Rejects a seller payout profile change request without changing the live payout profile.")
            .RequireAuthorization(MabuntlePolicies.FinanceApprove)
            .Produces<AdminPayoutProfileChangeRequestResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetSellerStateAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var state = await CreateSellerStateAsync(seller, dbContext, cancellationToken);
        return state is null ? SellerMustBeVerified() : HttpResults.Ok(state);
    }

    private static async Task<IResult> UpsertSellerDraftAsync(
        SellerPayoutProfileChangeRequestRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (seller.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return SellerMustBeVerified();
        }

        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == seller.Id, cancellationToken);
        if (payoutProfile is null || !payoutProfile.IsAdminApproved)
        {
            return PayoutProfileMissing();
        }

        var proposedReference = request.PayoutProviderReference.Trim();
        if (string.Equals(payoutProfile.PayoutProviderReference, proposedReference, StringComparison.Ordinal))
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.PayoutProviderReference)] = ["The proposed payout provider reference matches the current approved reference."]
            });
        }

        var activeRequest = await dbContext.SellerPayoutProfileChangeRequests
            .SingleOrDefaultAsync(
                item => item.SellerId == seller.Id
                    && (item.Status == SellerPayoutProfileChangeRequestStatus.Draft
                        || item.Status == SellerPayoutProfileChangeRequestStatus.PendingReview),
                cancellationToken);

        if (activeRequest?.Status == SellerPayoutProfileChangeRequestStatus.PendingReview)
        {
            return PendingRequestExists();
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var actionType = "PayoutProfileChangeDraftCreated";
        if (activeRequest is null)
        {
            activeRequest = new SellerPayoutProfileChangeRequest(
                seller.Id,
                proposedReference,
                request.Reason,
                actorUserId.Value);
            dbContext.SellerPayoutProfileChangeRequests.Add(activeRequest);
        }
        else
        {
            activeRequest.UpdateDraft(proposedReference, request.Reason);
            actionType = "PayoutProfileChangeDraftUpdated";
        }

        await RecordAuditAsync(
            auditLogService,
            actorUserId.Value,
            MabuntleRoles.Seller,
            actionType,
            activeRequest,
            request.Reason,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateSellerStateAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> SubmitSellerDraftAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (seller.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return SellerMustBeVerified();
        }

        var draft = await dbContext.SellerPayoutProfileChangeRequests
            .SingleOrDefaultAsync(
                item => item.SellerId == seller.Id
                    && item.Status == SellerPayoutProfileChangeRequestStatus.Draft,
                cancellationToken);
        if (draft is null)
        {
            return HttpResults.Problem(
                title: "SellerPayoutProfileChange.NoDraft",
                detail: "Create a payout profile change request before submitting it for review.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        draft.Submit(timeProvider.GetUtcNow());
        await RecordAuditAsync(
            auditLogService,
            actorUserId.Value,
            MabuntleRoles.Seller,
            "PayoutProfileChangeSubmitted",
            draft,
            draft.Reason,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateSellerStateAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> CancelSellerRequestAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var activeRequest = await dbContext.SellerPayoutProfileChangeRequests
            .SingleOrDefaultAsync(
                item => item.SellerId == seller.Id
                    && (item.Status == SellerPayoutProfileChangeRequestStatus.Draft
                        || item.Status == SellerPayoutProfileChangeRequestStatus.PendingReview),
                cancellationToken);
        if (activeRequest is null)
        {
            return HttpResults.Problem(
                title: "SellerPayoutProfileChange.NotFound",
                detail: "No active payout profile change request was found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        activeRequest.Cancel(timeProvider.GetUtcNow());
        await RecordAuditAsync(
            auditLogService,
            actorUserId.Value,
            MabuntleRoles.Seller,
            "PayoutProfileChangeCancelled",
            activeRequest,
            activeRequest.Reason,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateSellerStateAsync(seller, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetAdminQueueAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var requests = await dbContext.SellerPayoutProfileChangeRequests
            .Where(request => request.Status == SellerPayoutProfileChangeRequestStatus.PendingReview)
            .OrderBy(request => request.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var responses = new List<AdminPayoutProfileChangeRequestResponse>();
        foreach (var request in requests)
        {
            responses.Add(await CreateAdminResponseAsync(request, dbContext, cancellationToken));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetAdminDetailAsync(
        Guid requestId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.SellerPayoutProfileChangeRequests
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);

        return request is null
            ? ChangeRequestNotFound()
            : HttpResults.Ok(await CreateAdminResponseAsync(request, dbContext, cancellationToken));
    }

    private static async Task<IResult> ApproveAsync(
        Guid requestId,
        PayoutProfileChangeReviewRequest reviewRequest,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ReviewAsync(
            requestId,
            reviewRequest,
            principal,
            httpContext,
            dbContext,
            auditLogService,
            timeProvider,
            approve: true,
            cancellationToken);
    }

    private static async Task<IResult> RejectAsync(
        Guid requestId,
        PayoutProfileChangeReviewRequest reviewRequest,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ReviewAsync(
            requestId,
            reviewRequest,
            principal,
            httpContext,
            dbContext,
            auditLogService,
            timeProvider,
            approve: false,
            cancellationToken);
    }

    private static async Task<IResult> ReviewAsync(
        Guid requestId,
        PayoutProfileChangeReviewRequest reviewRequest,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewRequest.Reason))
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(reviewRequest.Reason)] = ["Reason is required."]
            });
        }

        var actorUserId = GetActorUserId(principal);
        if (!actorUserId.HasValue)
        {
            return ActorNotFound();
        }

        var request = await dbContext.SellerPayoutProfileChangeRequests
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (request is null)
        {
            return ChangeRequestNotFound();
        }

        if (request.RequestedByUserId == actorUserId.Value)
        {
            return HttpResults.Problem(
                title: "SellerPayoutProfileChange.DualControlRequired",
                detail: "The user who requested a payout profile change cannot approve or reject it.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == request.SellerId, cancellationToken);
        if (approve && payoutProfile is null)
        {
            return PayoutProfileMissing();
        }

        try
        {
            if (approve)
            {
                request.Approve(actorUserId.Value, reviewRequest.Reason, timeProvider.GetUtcNow());
                payoutProfile!.ReplaceProviderReferenceAndApprove(
                    request.ProposedPayoutProviderReference,
                    actorUserId.Value,
                    request.ReviewedAtUtc!.Value);
            }
            else
            {
                request.Reject(actorUserId.Value, reviewRequest.Reason, timeProvider.GetUtcNow());
            }
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Problem(
                title: "SellerPayoutProfileChange.InvalidState",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await RecordAuditAsync(
            auditLogService,
            actorUserId.Value,
            GetFinanceActorRole(principal),
            approve ? "PayoutProfileChangeApproved" : "PayoutProfileChangeRejected",
            request,
            reviewRequest.Reason,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return HttpResults.Problem(
                title: "SellerPayoutProfileChange.ConcurrentUpdate",
                detail: "The payout profile change request was already changed by another process.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return HttpResults.Ok(await CreateAdminResponseAsync(request, dbContext, cancellationToken));
    }

    private static async Task<SellerPayoutProfileChangeStateResponse?> CreateSellerStateAsync(
        SellerProfile seller,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (seller.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return null;
        }

        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == seller.Id, cancellationToken);
        var activeRequest = await dbContext.SellerPayoutProfileChangeRequests
            .Where(request => request.SellerId == seller.Id
                && (request.Status == SellerPayoutProfileChangeRequestStatus.Draft
                    || request.Status == SellerPayoutProfileChangeRequestStatus.PendingReview))
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var latestRequest = await dbContext.SellerPayoutProfileChangeRequests
            .Where(request => request.SellerId == seller.Id)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new SellerPayoutProfileChangeStateResponse(
            payoutProfile is null
                ? null
                : new SellerPayoutProfileSummaryResponse(
                    payoutProfile.PayoutProviderReference,
                    payoutProfile.IsAdminApproved,
                    payoutProfile.ApprovedAtUtc,
                    payoutProfile.ApprovedByUserId),
            activeRequest is null ? null : ToSellerResponse(activeRequest),
            latestRequest is null ? null : ToSellerResponse(latestRequest));
    }

    private static async Task<AdminPayoutProfileChangeRequestResponse> CreateAdminResponseAsync(
        SellerPayoutProfileChangeRequest request,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles.SingleAsync(seller => seller.Id == request.SellerId, cancellationToken);
        var payoutProfile = await dbContext.SellerPayoutProfiles
            .SingleOrDefaultAsync(profile => profile.SellerId == request.SellerId, cancellationToken);

        return new AdminPayoutProfileChangeRequestResponse(
            request.Id,
            request.SellerId,
            seller.DisplayName,
            seller.ContactEmail,
            seller.VerificationStatus.ToString(),
            payoutProfile?.PayoutProviderReference,
            payoutProfile?.IsAdminApproved == true,
            request.ProposedPayoutProviderReference,
            request.Reason,
            request.Status.ToString(),
            request.RequestedByUserId,
            request.SubmittedAtUtc,
            request.CancelledAtUtc,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.ReviewReason,
            request.CreatedAtUtc,
            request.UpdatedAtUtc);
    }

    private static SellerPayoutProfileChangeRequestResponse ToSellerResponse(
        SellerPayoutProfileChangeRequest request) =>
        new(
            request.Id,
            request.Status.ToString(),
            request.ProposedPayoutProviderReference,
            request.Reason,
            request.ReviewReason,
            request.SubmittedAtUtc,
            request.CancelledAtUtc,
            request.ReviewedAtUtc,
            request.CreatedAtUtc,
            request.UpdatedAtUtc);

    private static Dictionary<string, string[]> ValidateRequest(SellerPayoutProfileChangeRequestRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(request.PayoutProviderReference), request.PayoutProviderReference);
        AddRequired(errors, nameof(request.Reason), request.Reason);

        if (!string.IsNullOrWhiteSpace(request.PayoutProviderReference)
            && request.PayoutProviderReference.Trim().Length > SellerPayoutProfileChangeRequest.PayoutProviderReferenceMaxLength)
        {
            errors[nameof(request.PayoutProviderReference)] = [$"Payout provider reference cannot exceed {SellerPayoutProfileChangeRequest.PayoutProviderReferenceMaxLength} characters."];
        }

        if (!string.IsNullOrWhiteSpace(request.Reason)
            && request.Reason.Trim().Length > SellerPayoutProfileChangeRequest.ReasonMaxLength)
        {
            errors[nameof(request.Reason)] = [$"Reason cannot exceed {SellerPayoutProfileChangeRequest.ReasonMaxLength} characters."];
        }

        return errors;
    }

    private static void AddRequired(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
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

    private static Guid? GetActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private static string GetFinanceActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : principal.IsInRole(MabuntleRoles.FinanceApprover)
                ? MabuntleRoles.FinanceApprover
                : principal.IsInRole(MabuntleRoles.FinanceOperator)
                    ? MabuntleRoles.FinanceOperator
                    : MabuntleRoles.Admin;

    private static async Task RecordAuditAsync(
        IAuditLogService auditLogService,
        Guid actorUserId,
        string actorRole,
        string actionType,
        SellerPayoutProfileChangeRequest request,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId.ToString(),
                actorRole,
                actionType,
                "SellerPayoutProfileChangeRequest",
                request.Id.ToString(),
                JsonSerializer.Serialize(new { status = request.Status.ToString() }),
                JsonSerializer.Serialize(new
                {
                    status = request.Status.ToString(),
                    request.SellerId,
                    request.ProposedPayoutProviderReference
                }),
                reason,
                ipAddress),
            cancellationToken);
    }

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.ActorNotFound",
            detail: "The authenticated user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult SellerMustBeVerified() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.VerifiedSellerRequired",
            detail: "Only verified sellers can request payout profile changes. Use seller onboarding before verification.",
            statusCode: StatusCodes.Status409Conflict);

    private static IResult PayoutProfileMissing() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.PayoutProfileMissing",
            detail: "The seller does not have an approved payout profile.",
            statusCode: StatusCodes.Status409Conflict);

    private static IResult PendingRequestExists() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.PendingRequestExists",
            detail: "A payout profile change request is already pending finance review.",
            statusCode: StatusCodes.Status409Conflict);

    private static IResult ChangeRequestNotFound() =>
        HttpResults.Problem(
            title: "SellerPayoutProfileChange.NotFound",
            detail: "Payout profile change request was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record SellerPayoutProfileChangeRequestRequest(
    string PayoutProviderReference,
    string Reason);

public sealed record PayoutProfileChangeReviewRequest(string Reason);

public sealed record SellerPayoutProfileChangeStateResponse(
    SellerPayoutProfileSummaryResponse? CurrentPayoutProfile,
    SellerPayoutProfileChangeRequestResponse? ActiveRequest,
    SellerPayoutProfileChangeRequestResponse? LatestRequest);

public sealed record SellerPayoutProfileSummaryResponse(
    string PayoutProviderReference,
    bool IsAdminApproved,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId);

public sealed record SellerPayoutProfileChangeRequestResponse(
    Guid RequestId,
    string Status,
    string ProposedPayoutProviderReference,
    string Reason,
    string? ReviewReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminPayoutProfileChangeRequestResponse(
    Guid RequestId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerContactEmail,
    string SellerVerificationStatus,
    string? CurrentPayoutProviderReference,
    bool CurrentPayoutIsAdminApproved,
    string ProposedPayoutProviderReference,
    string Reason,
    string Status,
    Guid RequestedByUserId,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
