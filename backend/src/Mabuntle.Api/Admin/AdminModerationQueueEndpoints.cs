using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Admin;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminModerationQueueEndpoints
{
    private static readonly string[] Queues = ["Sellers", "Products", "Ads"];

    public static IEndpointRouteBuilder MapAdminModerationQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/moderation-queue")
            .WithTags("Admin Moderation Queue")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("/views", GetViewsAsync)
            .WithName("GetAdminQueueSavedViews")
            .Produces<IReadOnlyCollection<AdminQueueSavedViewResponse>>(StatusCodes.Status200OK);

        group.MapPost("/views", CreateViewAsync)
            .WithName("CreateAdminQueueSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/views/{viewId:guid}", UpdateViewAsync)
            .WithName("UpdateAdminQueueSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/views/{viewId:guid}", DeleteViewAsync)
            .WithName("DeleteAdminQueueSavedView")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/views/{viewId:guid}/make-default", MakeDefaultAsync)
            .WithName("MakeDefaultAdminQueueSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetAdminModerationQueueSummary")
            .Produces<AdminQueueSummaryResponse>(StatusCodes.Status200OK);

        return app;
    }

    public static async Task<AdminQueueSavedView?> GetSavedViewForRequestAsync(
        Guid? savedViewId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!savedViewId.HasValue || !TryGetActorUserId(principal, out var adminUserId))
        {
            return null;
        }

        return await dbContext.AdminQueueSavedViews
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == savedViewId.Value && item.AdminUserId == adminUserId, cancellationToken);
    }

    public static string? Merge(string? requestValue, string? savedValue) =>
        string.IsNullOrWhiteSpace(requestValue) ? savedValue : requestValue;

    public static Guid? Merge(Guid? requestValue, Guid? savedValue) => requestValue ?? savedValue;

    public static bool? Merge(bool? requestValue, bool? savedValue) => requestValue ?? savedValue;

    public static int? Merge(int? requestValue, int? savedValue) => requestValue ?? savedValue;

    private static async Task<IResult> GetViewsAsync(
        string? queue,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var adminUserId))
        {
            return ActorNotFound();
        }

        var normalizedQueue = NormalizeQueueOrNull(queue);
        var query = dbContext.AdminQueueSavedViews
            .AsNoTracking()
            .Where(item => item.AdminUserId == adminUserId);
        if (normalizedQueue is not null)
        {
            query = query.Where(item => item.Queue == normalizedQueue);
        }

        var views = await query
            .OrderBy(item => item.Queue)
            .ThenByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(views.Select(MapView).ToArray());
    }

    private static async Task<IResult> CreateViewAsync(
        AdminQueueSavedViewRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var adminUserId))
        {
            return ActorNotFound();
        }

        if (!TryBuildFilters(request, out var queue, out var filters, out var validation))
        {
            return validation!;
        }

        AdminQueueSavedView view;
        try
        {
            view = new AdminQueueSavedView(adminUserId, queue, request.Name ?? string.Empty, filters, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation("name", exception.Message);
        }

        if (request.IsDefault == true)
        {
            await ClearDefaultViewsAsync(adminUserId, queue, dbContext, timeProvider.GetUtcNow(), cancellationToken);
            view.MarkDefault(timeProvider.GetUtcNow());
        }

        dbContext.AdminQueueSavedViews.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Created($"/api/admin/moderation-queue/views/{view.Id}", MapView(view));
    }

    private static async Task<IResult> UpdateViewAsync(
        Guid viewId,
        AdminQueueSavedViewRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var adminUserId))
        {
            return ActorNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == adminUserId, cancellationToken);
        if (view is null)
        {
            return ViewNotFound();
        }

        if (!TryBuildFilters(request, out var queue, out var filters, out var validation))
        {
            return validation!;
        }

        if (view.Queue != queue)
        {
            return Validation("queue", "A saved view cannot be moved to another queue.");
        }

        try
        {
            view.RenameAndUpdate(request.Name ?? string.Empty, filters, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation("name", exception.Message);
        }

        if (request.IsDefault == true)
        {
            await ClearDefaultViewsAsync(adminUserId, view.Queue, dbContext, timeProvider.GetUtcNow(), cancellationToken);
            view.MarkDefault(timeProvider.GetUtcNow());
        }
        else if (request.IsDefault == false)
        {
            view.ClearDefault(timeProvider.GetUtcNow());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(MapView(view));
    }

    private static async Task<IResult> DeleteViewAsync(
        Guid viewId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var adminUserId))
        {
            return ActorNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == adminUserId, cancellationToken);
        if (view is null)
        {
            return ViewNotFound();
        }

        dbContext.AdminQueueSavedViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.NoContent();
    }

    private static async Task<IResult> MakeDefaultAsync(
        Guid viewId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var adminUserId))
        {
            return ActorNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == adminUserId, cancellationToken);
        if (view is null)
        {
            return ViewNotFound();
        }

        await ClearDefaultViewsAsync(adminUserId, view.Queue, dbContext, timeProvider.GetUtcNow(), cancellationToken);
        view.MarkDefault(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(MapView(view));
    }

    private static async Task<IResult> GetSummaryAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var queueItems = await BuildQueueSummaryItemsAsync(dbContext, now, cancellationToken);
        var triages = await AdminQueueTriageEndpoints.GetTriageSummariesAsync(
            dbContext,
            queueItems.Select(item => new AdminQueueItemKey(item.ItemType, item.ItemId)),
            cancellationToken);

        var enriched = queueItems.Select(item =>
        {
            triages.TryGetValue(new AdminQueueItemKey(item.ItemType, item.ItemId), out var triage);
            var sla = AdminQueueSla.Calculate(item.ItemType, item.SubmittedAtUtc, item.UpdatedAtUtc, now);
            return new AdminQueueSummaryItem(item.ItemType, item.Status, triage?.Priority ?? AdminQueuePriority.Normal.ToString(), triage?.AssignedToUserId, triage?.AssignedToDisplayName, sla.SlaStatus);
        }).ToArray();

        var reviewedActions = new[]
        {
            "SellerApproved", "SellerRejected", "SellerSuspended",
            "ProductApproved", "ProductRejected", "ProductChangesRequested",
            "ProductListingRevisionApproved", "ProductListingRevisionRejected",
            "ProductVariantRevisionApproved", "ProductVariantRevisionRejected",
            "AdCampaignApproved", "AdCampaignRejected"
        };
        var sevenDaysAgo = now.AddDays(-7);
        var today = now.Date;
        var reviews = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(item => reviewedActions.Contains(item.ActionType) && item.CreatedAtUtc >= sevenDaysAgo)
            .Select(item => new { item.ActionType, item.EntityType, item.EntityId, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var summary = new AdminQueueSummaryResponse(
            now,
            enriched.GroupBy(item => item.ItemType.ToString()).Select(group => new AdminQueueCountResponse(group.Key, group.Count())).OrderBy(item => item.Key).ToArray(),
            enriched.GroupBy(item => item.Status).Select(group => new AdminQueueCountResponse(group.Key, group.Count())).OrderBy(item => item.Key).ToArray(),
            enriched.GroupBy(item => item.Priority).Select(group => new AdminQueueCountResponse(group.Key, group.Count())).OrderBy(item => item.Key).ToArray(),
            enriched.GroupBy(item => item.SlaStatus).Select(group => new AdminQueueCountResponse(group.Key, group.Count())).OrderBy(item => item.Key).ToArray(),
            enriched.GroupBy(item => item.AssignedToUserId?.ToString() ?? "Unassigned").Select(group => new AdminQueueAssigneeCountResponse(group.Key, group.First().AssignedToDisplayName, group.Count())).OrderBy(item => item.AssignedToDisplayName ?? item.AssignedToUserId).ToArray(),
            reviews.Count(item => item.CreatedAtUtc.Date == today),
            reviews.Count,
            await CalculateAverageReviewHoursAsync(reviews.Select(item => new ReviewAuditSource(item.EntityType, item.EntityId, item.CreatedAtUtc)).ToArray(), dbContext, cancellationToken));

        return HttpResults.Ok(summary);
    }

    private static async Task<IReadOnlyCollection<QueueSummarySourceItem>> BuildQueueSummaryItemsAsync(
        MabuntleDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var items = new List<QueueSummarySourceItem>();
        items.AddRange(await dbContext.SellerProfiles
            .AsNoTracking()
            .Select(item => new QueueSummarySourceItem(AdminQueueItemType.Seller, item.Id, item.VerificationStatus.ToString(), null, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
        items.AddRange(await dbContext.Products
            .AsNoTracking()
            .Select(item => new QueueSummarySourceItem(AdminQueueItemType.Product, item.Id, item.Status.ToString(), item.Status == ProductStatus.PendingReview || item.Status == ProductStatus.NeedsAdminReview ? item.UpdatedAtUtc : null, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
        items.AddRange(await dbContext.ProductListingRevisions
            .AsNoTracking()
            .Select(item => new QueueSummarySourceItem(AdminQueueItemType.ListingRevision, item.Id, item.Status.ToString(), item.SubmittedAtUtc, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
        items.AddRange(await dbContext.ProductVariantRevisions
            .AsNoTracking()
            .Select(item => new QueueSummarySourceItem(AdminQueueItemType.VariantRevision, item.Id, item.Status.ToString(), item.SubmittedAtUtc, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
        items.AddRange(await dbContext.AdCampaigns
            .AsNoTracking()
            .Select(item => new QueueSummarySourceItem(AdminQueueItemType.AdCampaign, item.Id, item.Status.ToString(), item.SubmittedAtUtc, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken));
        return items;
    }

    private static async Task<double?> CalculateAverageReviewHoursAsync(
        IReadOnlyCollection<ReviewAuditSource> reviews,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (reviews.Count == 0)
        {
            return null;
        }

        var durations = new List<double>();
        foreach (var review in reviews)
        {
            if (!Guid.TryParse(review.EntityId, out var entityId))
            {
                continue;
            }

            var submittedAtUtc = await FindSubmittedAtUtcAsync(review.EntityType, entityId, review.ReviewedAtUtc, dbContext, cancellationToken);
            if (submittedAtUtc.HasValue && review.ReviewedAtUtc >= submittedAtUtc.Value)
            {
                durations.Add((review.ReviewedAtUtc - submittedAtUtc.Value).TotalHours);
            }
        }

        return durations.Count == 0 ? null : Math.Round(durations.Average(), 1);
    }

    private static async Task<DateTimeOffset?> FindSubmittedAtUtcAsync(
        string entityType,
        Guid entityId,
        DateTimeOffset reviewedAtUtc,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        entityType switch
        {
            "SellerProfile" => await dbContext.SellerVerifications
                .AsNoTracking()
                .Where(item => item.SellerId == entityId && item.SubmittedAtUtc <= reviewedAtUtc)
                .OrderByDescending(item => item.SubmittedAtUtc)
                .Select(item => (DateTimeOffset?)item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken),
            "Product" => await dbContext.Products
                .AsNoTracking()
                .Where(item => item.Id == entityId)
                .Select(item => (DateTimeOffset?)item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken),
            "ProductListingRevision" => await dbContext.ProductListingRevisions
                .AsNoTracking()
                .Where(item => item.Id == entityId)
                .Select(item => item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken),
            "ProductVariantRevision" => await dbContext.ProductVariantRevisions
                .AsNoTracking()
                .Where(item => item.Id == entityId)
                .Select(item => item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken),
            "AdCampaign" => await dbContext.AdCampaigns
                .AsNoTracking()
                .Where(item => item.Id == entityId)
                .Select(item => item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };

    private static async Task ClearDefaultViewsAsync(
        Guid adminUserId,
        string queue,
        MabuntleDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingDefaults = await dbContext.AdminQueueSavedViews
            .Where(item => item.AdminUserId == adminUserId && item.Queue == queue && item.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var view in existingDefaults)
        {
            view.ClearDefault(now);
        }
    }

    private static bool TryBuildFilters(
        AdminQueueSavedViewRequest request,
        out string queue,
        out AdminQueueSavedViewFilters filters,
        out IResult? validation)
    {
        queue = NormalizeQueueOrNull(request.Queue) ?? string.Empty;
        filters = new AdminQueueSavedViewFilters(null, null, null, null, null, null, null, null, null, null, null);
        validation = null;

        if (string.IsNullOrWhiteSpace(queue))
        {
            validation = Validation("queue", "Queue must be Sellers, Products, or Ads.");
            return false;
        }

        if (!AdminQueueSla.IsKnownStatus(request.Filters?.Sla))
        {
            validation = Validation("sla", "SLA filter must be OnTrack, DueSoon, or Overdue.");
            return false;
        }

        filters = new AdminQueueSavedViewFilters(
            request.Filters?.View,
            request.Filters?.Status,
            request.Filters?.Category,
            request.Filters?.Search,
            request.Filters?.SellerId,
            request.Filters?.Assigned,
            request.Filters?.Priority,
            request.Filters?.HasNotes,
            request.Filters?.Sla,
            request.Filters?.Sort,
            request.Filters?.PageSize);
        return true;
    }

    private static string? NormalizeQueueOrNull(string? queue)
    {
        if (string.IsNullOrWhiteSpace(queue))
        {
            return null;
        }

        var normalized = queue.Trim();
        return Queues.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static AdminQueueSavedViewResponse MapView(AdminQueueSavedView view) =>
        new(
            view.Id,
            view.Queue,
            view.Name,
            view.IsDefault,
            new AdminQueueSavedViewFiltersResponse(
                view.View,
                view.Status,
                view.Category,
                view.Search,
                view.SellerId,
                view.Assigned,
                view.Priority,
                view.HasNotes,
                view.Sla,
                view.Sort,
                view.PageSize),
            view.CreatedAtUtc,
            view.UpdatedAtUtc);

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid actorUserId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "AdminQueue.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult ViewNotFound() =>
        HttpResults.Problem(
            title: "AdminQueue.SavedViewNotFound",
            detail: "The saved queue view was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record QueueSummarySourceItem(AdminQueueItemType ItemType, Guid ItemId, string Status, DateTimeOffset? SubmittedAtUtc, DateTimeOffset UpdatedAtUtc);

    private sealed record AdminQueueSummaryItem(AdminQueueItemType ItemType, string Status, string Priority, Guid? AssignedToUserId, string? AssignedToDisplayName, string SlaStatus);

    private sealed record ReviewAuditSource(string EntityType, string? EntityId, DateTimeOffset ReviewedAtUtc);
}

public sealed record AdminQueueSavedViewRequest(
    string? Queue,
    string? Name,
    bool? IsDefault,
    AdminQueueSavedViewFiltersRequest? Filters);

public sealed record AdminQueueSavedViewFiltersRequest(
    string? View,
    string? Status,
    string? Category,
    string? Search,
    Guid? SellerId,
    string? Assigned,
    string? Priority,
    bool? HasNotes,
    string? Sla,
    string? Sort,
    int? PageSize);

public sealed record AdminQueueSavedViewResponse(
    Guid ViewId,
    string Queue,
    string Name,
    bool IsDefault,
    AdminQueueSavedViewFiltersResponse Filters,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminQueueSavedViewFiltersResponse(
    string? View,
    string? Status,
    string? Category,
    string? Search,
    Guid? SellerId,
    string? Assigned,
    string? Priority,
    bool? HasNotes,
    string? Sla,
    string? Sort,
    int PageSize);

public sealed record AdminQueueSummaryResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<AdminQueueCountResponse> ItemTypeCounts,
    IReadOnlyCollection<AdminQueueCountResponse> StatusCounts,
    IReadOnlyCollection<AdminQueueCountResponse> PriorityCounts,
    IReadOnlyCollection<AdminQueueCountResponse> SlaCounts,
    IReadOnlyCollection<AdminQueueAssigneeCountResponse> AssigneeCounts,
    int ReviewedToday,
    int ReviewedLast7Days,
    double? AverageReviewHours);

public sealed record AdminQueueCountResponse(string Key, int Count);

public sealed record AdminQueueAssigneeCountResponse(string AssignedToUserId, string? AssignedToDisplayName, int Count);
