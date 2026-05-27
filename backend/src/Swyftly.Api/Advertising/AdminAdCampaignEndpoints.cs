using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Admin;
using Swyftly.Api.Notifications;
using Swyftly.Application.Admin;
using Swyftly.Application.Advertising;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Admin;
using Swyftly.Domain.Advertising;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Advertising;

public static class AdminAdCampaignEndpoints
{
    public static IEndpointRouteBuilder MapAdminAdCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/ad-campaigns")
            .WithTags("Admin Ad Campaigns")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        group.MapGet("", GetAllAsync)
            .WithName("GetAdminAdCampaigns")
            .WithSummary("Returns ad campaigns for operational admin review.")
            .Produces<AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingAdCampaigns")
            .Produces<IReadOnlyCollection<AdminAdCampaignSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetAdminAdCampaign")
            .Produces<AdminAdCampaignDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/approve", ApproveAsync)
            .WithName("ApproveAdCampaign")
            .Produces<AdminAdCampaignDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reject", RejectAsync)
            .WithName("RejectAdCampaign")
            .Produces<AdminAdCampaignDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string? view,
        string? status,
        string? search,
        Guid? sellerId,
        string? assigned,
        string? priority,
        bool? hasNotes,
        string? sla,
        Guid? savedViewId,
        int? page,
        int? pageSize,
        string? sort,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var savedView = await AdminModerationQueueEndpoints.GetSavedViewForRequestAsync(savedViewId, principal, dbContext, cancellationToken);
        view = AdminModerationQueueEndpoints.Merge(view, savedView?.View);
        status = AdminModerationQueueEndpoints.Merge(status, savedView?.Status);
        search = AdminModerationQueueEndpoints.Merge(search, savedView?.Search);
        sellerId = AdminModerationQueueEndpoints.Merge(sellerId, savedView?.SellerId);
        assigned = AdminModerationQueueEndpoints.Merge(assigned, savedView?.Assigned);
        priority = AdminModerationQueueEndpoints.Merge(priority, savedView?.Priority);
        hasNotes = AdminModerationQueueEndpoints.Merge(hasNotes, savedView?.HasNotes);
        sla = AdminModerationQueueEndpoints.Merge(sla, savedView?.Sla);
        pageSize = AdminModerationQueueEndpoints.Merge(pageSize, savedView?.PageSize);
        sort = AdminModerationQueueEndpoints.Merge(sort, savedView?.Sort);

        if (!AdminQueueSla.IsKnownStatus(sla))
        {
            return Validation("sla", "SLA filter must be OnTrack, DueSoon, or Overdue.");
        }

        var pageNumber = Math.Max(page ?? 1, 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var normalizedView = string.Equals(view, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "NeedsAttention";
        var normalizedSearch = search?.Trim();
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "UpdatedDesc" : sort.Trim();
        var now = timeProvider.GetUtcNow();

        AdCampaignStatus? requestedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AdCampaignStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return Validation("status", "Unknown ad campaign status.");
            }

            requestedStatus = parsedStatus;
        }

        var campaigns = await dbContext.AdCampaigns
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var campaignIds = campaigns.Select(item => item.Id).ToHashSet();
        var sellerProfiles = await dbContext.SellerProfiles
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var productCounts = await dbContext.AdCampaignProducts
            .AsNoTracking()
            .Where(item => campaignIds.Contains(item.AdCampaignId))
            .GroupBy(item => item.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CampaignId, item => item.Count, cancellationToken);
        var budgets = await dbContext.AdBudgets
            .AsNoTracking()
            .Where(item => campaignIds.Contains(item.AdCampaignId))
            .ToDictionaryAsync(item => item.AdCampaignId, cancellationToken);

        IEnumerable<AdminAdCampaignOperationalSummaryResponse> items = campaigns.Select(campaign =>
        {
            sellerProfiles.TryGetValue(campaign.SellerId, out var seller);
            productCounts.TryGetValue(campaign.Id, out var productCount);
            budgets.TryGetValue(campaign.Id, out var budget);
            return new AdminAdCampaignOperationalSummaryResponse(
                campaign.Id,
                campaign.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                campaign.Name,
                campaign.CampaignType.ToString(),
                campaign.Status.ToString(),
                campaign.StartsAtUtc,
                campaign.EndsAtUtc,
                campaign.SubmittedAtUtc,
                campaign.UpdatedAtUtc,
                productCount,
                budget?.TotalBudget,
                budget?.Currency,
                $"/admin/ads/{campaign.Id}");
        });

        if (sellerId.HasValue)
        {
            items = items.Where(item => item.SellerId == sellerId.Value);
        }

        if (requestedStatus is null && normalizedView == "NeedsAttention")
        {
            items = items.Where(item => item.Status == AdCampaignStatus.PendingReview.ToString());
        }

        var triageItemList = items.ToList();
        var triageSummaries = await AdminQueueTriageEndpoints.GetTriageSummariesAsync(
            dbContext,
            triageItemList.Select(item => new AdminQueueItemKey(AdminQueueItemType.AdCampaign, item.AdCampaignId)),
            cancellationToken);
        items = triageItemList.Select(item =>
        {
            triageSummaries.TryGetValue(new AdminQueueItemKey(AdminQueueItemType.AdCampaign, item.AdCampaignId), out var triage);
            var slaState = AdminQueueSla.Calculate(AdminQueueItemType.AdCampaign, item.SubmittedAtUtc, item.UpdatedAtUtc, now);
            return item with
            {
                AssignedToUserId = triage?.AssignedToUserId,
                AssignedToDisplayName = triage?.AssignedToDisplayName,
                Priority = triage?.Priority ?? AdminQueuePriority.Normal.ToString(),
                LatestTriageNote = triage?.LatestTriageNote,
                TriageNoteCount = triage?.TriageNoteCount ?? 0,
                TriageUpdatedAtUtc = triage?.TriageUpdatedAtUtc,
                AgeHours = slaState.AgeHours,
                SlaStatus = slaState.SlaStatus,
                SlaDueAtUtc = slaState.SlaDueAtUtc
            };
        });

        items = items.Where(item => AdminQueueSla.Matches(new AdminQueueSlaResponse(item.AgeHours, item.SlaStatus, item.SlaDueAtUtc ?? DateTimeOffset.MinValue), sla));

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            items = items.Where(item => TextMatches(
                normalizedSearch,
                item.Name,
                item.CampaignType,
                item.Status,
                item.SellerDisplayName,
                item.SellerVerificationStatus));
        }

        items = items.Where(item => AdminQueueTriageEndpoints.MatchesTriageFilters(
            new AdminQueueTriageSummaryResponse(item.AssignedToUserId, item.AssignedToDisplayName, item.Priority, item.LatestTriageNote, item.TriageNoteCount, item.TriageUpdatedAtUtc),
            assigned,
            priority,
            hasNotes,
            principal));

        var statusCountSource = items.ToList();
        if (requestedStatus.HasValue)
        {
            items = statusCountSource.Where(item => string.Equals(item.Status, requestedStatus.Value.ToString(), StringComparison.Ordinal));
        }

        items = normalizedSort.ToLowerInvariant() switch
        {
            "updatedasc" => items.OrderBy(item => item.UpdatedAtUtc),
            "submitteddesc" => items.OrderByDescending(item => item.SubmittedAtUtc ?? DateTimeOffset.MinValue),
            "submittedasc" => items.OrderBy(item => item.SubmittedAtUtc ?? DateTimeOffset.MaxValue),
            "nameasc" => items.OrderBy(item => item.Name),
            "namedesc" => items.OrderByDescending(item => item.Name),
            "statusasc" => items.OrderBy(item => item.Status),
            "statusdesc" => items.OrderByDescending(item => item.Status),
            _ => items.OrderByDescending(item => item.UpdatedAtUtc)
        };

        var orderedItems = items.ToList();
        var totalCount = orderedItems.Count;
        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .ToList();

        return HttpResults.Ok(new AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>(
            pagedItems,
            totalCount,
            pageNumber,
            requestedPageSize,
            BuildStatusCounts(statusCountSource.Select(item => item.Status))));
    }

    private static async Task<IResult> GetPendingAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.Status == AdCampaignStatus.PendingReview)
            .OrderBy(campaign => campaign.SubmittedAtUtc ?? campaign.UpdatedAtUtc)
            .Select(campaign => campaign.Id)
            .ToListAsync(cancellationToken);

        var responses = new List<AdminAdCampaignSummaryResponse>();
        foreach (var campaignId in campaignIds)
        {
            responses.Add(await CreateSummaryResponseAsync(campaignId, dbContext, cancellationToken));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        SwyftlyDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        CancellationToken cancellationToken)
    {
        var detail = await CreateDetailResponseAsync(id, dbContext, eligibilityService, cancellationToken);
        return detail is null ? CampaignNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns
            .Include(item => item.Products)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null)
        {
            return CampaignNotFound();
        }

        var eligibility = await eligibilityService.ValidateAsync(
            campaign.SellerId,
            campaign.Products.Select(product => product.ProductId).ToArray(),
            cancellationToken);
        if (!eligibility.IsEligible)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["eligibility"] = eligibility.AllReasons.ToArray()
            });
        }

        var previousStatus = campaign.Status;
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return ActorNotFound();
        }

        try
        {
            campaign.Approve(actorUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("campaign", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "AdCampaignApproved",
            campaign.Id,
            previousStatus,
            campaign.Status,
            reason: null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            campaign.SellerId,
            SellerNotificationTypes.AdCampaignApproved,
            "Ad campaign approved",
            $"Your ad campaign {campaign.Name} has been approved.",
            "AdCampaign",
            campaign.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminAdCampaignEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateDetailResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        AdminAdCampaignReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReasonRequired();
        }

        var campaign = await dbContext.AdCampaigns.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null)
        {
            return CampaignNotFound();
        }

        var previousStatus = campaign.Status;
        try
        {
            campaign.Reject(request.Reason, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("campaign", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "AdCampaignRejected",
            campaign.Id,
            previousStatus,
            campaign.Status,
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            campaign.SellerId,
            SellerNotificationTypes.AdCampaignRejected,
            "Ad campaign rejected",
            $"Your ad campaign {campaign.Name} was rejected. Reason: {request.Reason.Trim()}",
            "AdCampaign",
            campaign.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminAdCampaignEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateDetailResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<AdminAdCampaignSummaryResponse> CreateSummaryResponseAsync(
        Guid campaignId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns
            .AsNoTracking()
            .SingleAsync(item => item.Id == campaignId, cancellationToken);
        var seller = await dbContext.SellerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaign.SellerId, cancellationToken);
        var productCount = await dbContext.AdCampaignProducts
            .AsNoTracking()
            .CountAsync(item => item.AdCampaignId == campaign.Id, cancellationToken);
        var budget = await dbContext.AdBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AdCampaignId == campaign.Id, cancellationToken);

        return new AdminAdCampaignSummaryResponse(
            campaign.Id,
            campaign.SellerId,
            seller?.DisplayName,
            campaign.Name,
            campaign.CampaignType.ToString(),
            campaign.Status.ToString(),
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            campaign.SubmittedAtUtc,
            productCount,
            budget?.TotalBudget,
            budget?.Currency);
    }

    private static async Task<AdminAdCampaignDetailResponse?> CreateDetailResponseAsync(
        Guid campaignId,
        SwyftlyDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var seller = await dbContext.SellerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaign.SellerId, cancellationToken);
        var productIds = await dbContext.AdCampaignProducts
            .AsNoTracking()
            .Where(item => item.AdCampaignId == campaign.Id)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.ProductId)
            .ToListAsync(cancellationToken);
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new AdminAdCampaignProductResponse(
                product.Id,
                product.Title,
                product.Status.ToString(),
                product.PublishedAtUtc))
            .ToListAsync(cancellationToken);
        var budget = await dbContext.AdBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AdCampaignId == campaign.Id, cancellationToken);
        var eligibility = await eligibilityService.ValidateAsync(campaign.SellerId, productIds, cancellationToken);
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "AdCampaign" && auditLog.EntityId == campaign.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminAdCampaignDetailResponse(
            campaign.Id,
            campaign.SellerId,
            new AdminAdCampaignSellerResponse(
                seller?.DisplayName,
                seller?.ContactEmail,
                seller?.VerificationStatus.ToString()),
            campaign.Name,
            campaign.CampaignType.ToString(),
            campaign.Status.ToString(),
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            campaign.SubmittedAtUtc,
            campaign.ApprovedAtUtc,
            campaign.PausedAtUtc,
            campaign.CompletedAtUtc,
            campaign.CancelledAtUtc,
            campaign.RejectionReason,
            products,
            budget is null
                ? null
                : new AdminAdCampaignBudgetResponse(
                    budget.Currency,
                    budget.DailyBudget,
                    budget.TotalBudget,
                    budget.MaxCostPerClick,
                    budget.SpentAmount),
            new AdminAdCampaignEligibilityResponse(
                eligibility.IsEligible,
                eligibility.SellerReasons,
                eligibility.Products.Select(product => new AdminAdProductEligibilityResponse(
                    product.ProductId,
                    product.IsEligible,
                    product.QualityScore,
                    product.Reasons)).ToArray()),
            auditTrail);
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        Guid campaignId,
        AdCampaignStatus previousStatus,
        AdCampaignStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                GetActorRole(principal),
                actionType,
                "AdCampaign",
                campaignId.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { status = newStatus.ToString() }),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : SwyftlyRoles.Admin;

    private static IResult ReasonRequired() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reason"] = ["Reason is required."]
        });

    private static bool TextMatches(string search, params string?[] values)
    {
        var normalizedSearch = search.Trim().ToLowerInvariant();
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => value!.ToLowerInvariant().Contains(normalizedSearch, StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<AdminStatusCountResponse> BuildStatusCounts(IEnumerable<string> statuses) =>
        statuses
            .GroupBy(status => status, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => new AdminStatusCountResponse(group.Key, group.Count()))
            .ToArray();

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "AdminAdCampaigns.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult CampaignNotFound() =>
        HttpResults.Problem(
            title: "AdminAdCampaigns.CampaignNotFound",
            detail: "Ad campaign was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record AdminAdCampaignSummaryResponse(
    Guid AdCampaignId,
    Guid SellerId,
    string? SellerDisplayName,
    string Name,
    string CampaignType,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    int ProductCount,
    decimal? TotalBudget,
    string? Currency);

public sealed record AdminAdCampaignOperationalSummaryResponse(
    Guid AdCampaignId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerVerificationStatus,
    string Name,
    string CampaignType,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int ProductCount,
    decimal? TotalBudget,
    string? Currency,
    string DetailRoute,
    Guid? AssignedToUserId = null,
    string? AssignedToDisplayName = null,
    string Priority = "Normal",
    string? LatestTriageNote = null,
    int TriageNoteCount = 0,
    DateTimeOffset? TriageUpdatedAtUtc = null,
    int AgeHours = 0,
    string SlaStatus = "OnTrack",
    DateTimeOffset? SlaDueAtUtc = null);

public sealed record AdminAdCampaignDetailResponse(
    Guid AdCampaignId,
    Guid SellerId,
    AdminAdCampaignSellerResponse Seller,
    string Name,
    string CampaignType,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? RejectionReason,
    IReadOnlyCollection<AdminAdCampaignProductResponse> Products,
    AdminAdCampaignBudgetResponse? Budget,
    AdminAdCampaignEligibilityResponse Eligibility,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminAdCampaignSellerResponse(
    string? DisplayName,
    string? ContactEmail,
    string? VerificationStatus);

public sealed record AdminAdCampaignProductResponse(
    Guid ProductId,
    string? Title,
    string Status,
    DateTimeOffset? PublishedAtUtc);

public sealed record AdminAdCampaignBudgetResponse(
    string Currency,
    decimal DailyBudget,
    decimal TotalBudget,
    decimal MaxCostPerClick,
    decimal SpentAmount);

public sealed record AdminAdCampaignEligibilityResponse(
    bool IsEligible,
    IReadOnlyCollection<string> SellerReasons,
    IReadOnlyCollection<AdminAdProductEligibilityResponse> Products);

public sealed record AdminAdProductEligibilityResponse(
    Guid ProductId,
    bool IsEligible,
    int QualityScore,
    IReadOnlyCollection<string> Reasons);

public sealed record AdminAdCampaignReasonRequest(string Reason);
