using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Application.Identity;
using Swyftly.Domain.Admin;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Admin;

public static class AdminQueueTriageEndpoints
{
    public static IEndpointRouteBuilder MapAdminQueueTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/moderation-queue")
            .WithTags("Admin Moderation Queue")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

        group.MapGet("/items/{itemType}/{itemId:guid}/triage", GetAsync)
            .WithName("GetAdminQueueItemTriage")
            .Produces<AdminQueueTriageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/items/{itemType}/{itemId:guid}/triage", UpdateAsync)
            .WithName("UpdateAdminQueueItemTriage")
            .Produces<AdminQueueTriageResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/items/{itemType}/{itemId:guid}/claim", ClaimAsync)
            .WithName("ClaimAdminQueueItem")
            .Produces<AdminQueueTriageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/items/{itemType}/{itemId:guid}/unclaim", UnclaimAsync)
            .WithName("UnclaimAdminQueueItem")
            .Produces<AdminQueueTriageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/bulk-triage", BulkTriageAsync)
            .WithName("BulkTriageAdminQueueItems")
            .Produces<AdminQueueBulkTriageResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return app;
    }

    public static async Task<IReadOnlyDictionary<AdminQueueItemKey, AdminQueueTriageSummaryResponse>> GetTriageSummariesAsync(
        SwyftlyDbContext dbContext,
        IEnumerable<AdminQueueItemKey> itemKeys,
        CancellationToken cancellationToken)
    {
        var keys = itemKeys.Distinct().ToArray();
        if (keys.Length == 0)
        {
            return new Dictionary<AdminQueueItemKey, AdminQueueTriageSummaryResponse>();
        }

        var itemTypes = keys.Select(key => key.ItemType).Distinct().ToArray();
        var itemIds = keys.Select(key => key.ItemId).Distinct().ToArray();
        var triages = await dbContext.AdminQueueTriages
            .AsNoTracking()
            .Where(triage => itemTypes.Contains(triage.ItemType) && itemIds.Contains(triage.ItemId))
            .Select(triage => new
            {
                triage.ItemType,
                triage.ItemId,
                triage.AssignedToUserId,
                triage.Priority,
                triage.LatestNote,
                triage.UpdatedAtUtc,
                NoteCount = triage.Notes.Count
            })
            .ToListAsync(cancellationToken);

        var assignedUserIds = triages
            .Select(item => item.AssignedToUserId)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        var assignedNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => assignedUserIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.UserName ?? user.Email ?? user.Id.ToString(),
                cancellationToken);

        return triages.ToDictionary(
            item => new AdminQueueItemKey(item.ItemType, item.ItemId),
            item => new AdminQueueTriageSummaryResponse(
                item.AssignedToUserId,
                item.AssignedToUserId.HasValue && assignedNames.TryGetValue(item.AssignedToUserId.Value, out var displayName) ? displayName : null,
                item.Priority.ToString(),
                item.LatestNote,
                item.NoteCount,
                item.UpdatedAtUtc));
    }

    public static bool MatchesTriageFilters(
        AdminQueueTriageSummaryResponse? triage,
        string? assigned,
        string? priority,
        bool? hasNotes,
        ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(priority)
            && (!Enum.TryParse<AdminQueuePriority>(priority, ignoreCase: true, out var requestedPriority)
                || !string.Equals((triage?.Priority ?? AdminQueuePriority.Normal.ToString()), requestedPriority.ToString(), StringComparison.Ordinal)))
        {
            return false;
        }

        if (hasNotes.HasValue && ((triage?.TriageNoteCount ?? 0) > 0) != hasNotes.Value)
        {
            return false;
        }

        var normalizedAssigned = assigned?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAssigned) || string.Equals(normalizedAssigned, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalizedAssigned, "Unassigned", StringComparison.OrdinalIgnoreCase))
        {
            return triage?.AssignedToUserId is null;
        }

        if (string.Equals(normalizedAssigned, "Mine", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetActorUserId(principal, out var actorUserId) && triage?.AssignedToUserId == actorUserId;
        }

        return false;
    }

    private static async Task<IResult> GetAsync(
        string itemType,
        Guid itemId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseItemType(itemType, out var parsedItemType))
        {
            return Validation("itemType", "Unknown moderation queue item type.");
        }

        if (!await QueueItemExistsAsync(parsedItemType, itemId, dbContext, cancellationToken))
        {
            return ItemNotFound();
        }

        var triage = await dbContext.AdminQueueTriages
            .AsNoTracking()
            .Include(item => item.Notes)
            .SingleOrDefaultAsync(item => item.ItemType == parsedItemType && item.ItemId == itemId, cancellationToken);

        return HttpResults.Ok(await MapResponseAsync(parsedItemType, itemId, triage, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(
        string itemType,
        Guid itemId,
        AdminQueueTriageUpdateRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseItemType(itemType, out var parsedItemType))
        {
            return Validation("itemType", "Unknown moderation queue item type.");
        }

        if (!await QueueItemExistsAsync(parsedItemType, itemId, dbContext, cancellationToken))
        {
            return ItemNotFound();
        }

        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return ActorNotFound();
        }

        AdminQueuePriority? priority = null;
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            if (!Enum.TryParse<AdminQueuePriority>(request.Priority, ignoreCase: true, out var parsedPriority))
            {
                return Validation("priority", "Unknown triage priority.");
            }

            priority = parsedPriority;
        }

        var triage = await GetOrCreateTriageAsync(parsedItemType, itemId, actorUserId, dbContext, timeProvider, cancellationToken);
        var previous = CreateAuditSnapshot(triage);
        var now = timeProvider.GetUtcNow();

        if (request.ClearAssignment == true)
        {
            triage.Assign(null, now);
        }
        else if (request.AssignedToUserId.HasValue)
        {
            triage.Assign(request.AssignedToUserId.Value, now);
        }

        if (priority.HasValue)
        {
            triage.SetPriority(priority.Value, now);
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            try
            {
                var note = triage.AddNote(actorUserId, request.Note, now);
                dbContext.AdminQueueTriageNotes.Add(note);
            }
            catch (ArgumentException exception)
            {
                return Validation("note", exception.Message);
            }
        }

        await AddAuditLogAsync(auditLogService, principal, httpContext, "AdminQueueTriageUpdated", triage, previous, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await MapResponseAsync(parsedItemType, itemId, triage, dbContext, cancellationToken));
    }

    private static async Task<IResult> ClaimAsync(
        string itemType,
        Guid itemId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseItemType(itemType, out var parsedItemType))
        {
            return Validation("itemType", "Unknown moderation queue item type.");
        }

        if (!await QueueItemExistsAsync(parsedItemType, itemId, dbContext, cancellationToken))
        {
            return ItemNotFound();
        }

        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return ActorNotFound();
        }

        var triage = await GetOrCreateTriageAsync(parsedItemType, itemId, actorUserId, dbContext, timeProvider, cancellationToken);
        var previous = CreateAuditSnapshot(triage);
        try
        {
            triage.Claim(actorUserId, principal.IsInRole(SwyftlyRoles.SuperAdmin), timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Problem(
                title: "AdminQueueTriage.AlreadyClaimed",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await AddAuditLogAsync(auditLogService, principal, httpContext, "AdminQueueItemClaimed", triage, previous, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await MapResponseAsync(parsedItemType, itemId, triage, dbContext, cancellationToken));
    }

    private static async Task<IResult> UnclaimAsync(
        string itemType,
        Guid itemId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseItemType(itemType, out var parsedItemType))
        {
            return Validation("itemType", "Unknown moderation queue item type.");
        }

        if (!await QueueItemExistsAsync(parsedItemType, itemId, dbContext, cancellationToken))
        {
            return ItemNotFound();
        }

        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return ActorNotFound();
        }

        var triage = await GetOrCreateTriageAsync(parsedItemType, itemId, actorUserId, dbContext, timeProvider, cancellationToken);
        var previous = CreateAuditSnapshot(triage);
        triage.Unclaim(timeProvider.GetUtcNow());
        await AddAuditLogAsync(auditLogService, principal, httpContext, "AdminQueueItemUnclaimed", triage, previous, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await MapResponseAsync(parsedItemType, itemId, triage, dbContext, cancellationToken));
    }

    private static async Task<IResult> BulkTriageAsync(
        AdminQueueBulkTriageRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count is 0 or > 100)
        {
            return Validation("items", "Bulk triage requires between 1 and 100 items.");
        }

        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return ActorNotFound();
        }

        var action = request.Action?.Trim();
        if (string.IsNullOrWhiteSpace(action))
        {
            return Validation("action", "Bulk triage action is required.");
        }

        AdminQueuePriority? priority = null;
        if (string.Equals(action, "SetPriority", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<AdminQueuePriority>(request.Priority, ignoreCase: true, out var parsedPriority))
            {
                return Validation("priority", "A valid priority is required for SetPriority.");
            }

            priority = parsedPriority;
        }

        if (string.Equals(action, "AddNote", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Note))
        {
            return Validation("note", "Note is required for AddNote.");
        }

        if (!new[] { "SetPriority", "AddNote", "Claim", "Unclaim" }.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            return Validation("action", "Unsupported bulk triage action.");
        }

        var results = new List<AdminQueueBulkTriageItemResult>();
        foreach (var item in request.Items.DistinctBy(item => new { item.ItemType, item.ItemId }))
        {
            if (!TryParseItemType(item.ItemType, out var parsedItemType))
            {
                results.Add(new AdminQueueBulkTriageItemResult(item.ItemType, item.ItemId, false, "Unknown item type.", null));
                continue;
            }

            if (!await QueueItemExistsAsync(parsedItemType, item.ItemId, dbContext, cancellationToken))
            {
                results.Add(new AdminQueueBulkTriageItemResult(item.ItemType, item.ItemId, false, "Queue item was not found.", null));
                continue;
            }

            var triage = await GetOrCreateTriageAsync(parsedItemType, item.ItemId, actorUserId, dbContext, timeProvider, cancellationToken);
            var previous = CreateAuditSnapshot(triage);
            try
            {
                var now = timeProvider.GetUtcNow();
                if (string.Equals(action, "SetPriority", StringComparison.OrdinalIgnoreCase))
                {
                    triage.SetPriority(priority!.Value, now);
                }
                else if (string.Equals(action, "AddNote", StringComparison.OrdinalIgnoreCase))
                {
                    var note = triage.AddNote(actorUserId, request.Note!, now);
                    dbContext.AdminQueueTriageNotes.Add(note);
                }
                else if (string.Equals(action, "Claim", StringComparison.OrdinalIgnoreCase))
                {
                    triage.Claim(actorUserId, principal.IsInRole(SwyftlyRoles.SuperAdmin), now);
                }
                else
                {
                    triage.Unclaim(now);
                }

                await AddAuditLogAsync(auditLogService, principal, httpContext, $"AdminQueueBulk{action}", triage, previous, cancellationToken);
                results.Add(new AdminQueueBulkTriageItemResult(parsedItemType.ToString(), item.ItemId, true, null, await MapResponseAsync(parsedItemType, item.ItemId, triage, dbContext, cancellationToken)));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                results.Add(new AdminQueueBulkTriageItemResult(parsedItemType.ToString(), item.ItemId, false, exception.Message, null));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(new AdminQueueBulkTriageResponse(results.Count(item => item.IsSuccess), results.Count(item => !item.IsSuccess), results));
    }

    private static async Task<AdminQueueTriage> GetOrCreateTriageAsync(
        AdminQueueItemType itemType,
        Guid itemId,
        Guid actorUserId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var triage = await dbContext.AdminQueueTriages
            .Include(item => item.Notes)
            .SingleOrDefaultAsync(item => item.ItemType == itemType && item.ItemId == itemId, cancellationToken);
        if (triage is not null)
        {
            return triage;
        }

        triage = new AdminQueueTriage(itemType, itemId, actorUserId, timeProvider.GetUtcNow());
        dbContext.AdminQueueTriages.Add(triage);
        return triage;
    }

    private static async Task<bool> QueueItemExistsAsync(
        AdminQueueItemType itemType,
        Guid itemId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        itemType switch
        {
            AdminQueueItemType.Seller => await dbContext.SellerProfiles.AnyAsync(item => item.Id == itemId, cancellationToken),
            AdminQueueItemType.Product => await dbContext.Products.AnyAsync(item => item.Id == itemId, cancellationToken),
            AdminQueueItemType.ListingRevision => await dbContext.ProductListingRevisions.AnyAsync(item => item.Id == itemId, cancellationToken),
            AdminQueueItemType.VariantRevision => await dbContext.ProductVariantRevisions.AnyAsync(item => item.Id == itemId, cancellationToken),
            AdminQueueItemType.AdCampaign => await dbContext.AdCampaigns.AnyAsync(item => item.Id == itemId, cancellationToken),
            _ => false
        };

    private static async Task<AdminQueueTriageResponse> MapResponseAsync(
        AdminQueueItemType itemType,
        Guid itemId,
        AdminQueueTriage? triage,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var notes = triage?.Notes.OrderByDescending(item => item.CreatedAtUtc).ToArray() ?? [];
        var userIds = notes.Select(item => item.ActorUserId)
            .Concat(triage?.AssignedToUserId is null ? [] : [triage.AssignedToUserId.Value])
            .Distinct()
            .ToArray();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.UserName ?? user.Email ?? user.Id.ToString(), cancellationToken);

        return new AdminQueueTriageResponse(
            itemType.ToString(),
            itemId,
            triage?.AssignedToUserId,
            triage?.AssignedToUserId is { } assignedTo && users.TryGetValue(assignedTo, out var assignedName) ? assignedName : null,
            triage?.Priority.ToString() ?? AdminQueuePriority.Normal.ToString(),
            triage?.LatestNote,
            notes.Length,
            triage?.UpdatedAtUtc,
            notes.Select(note => new AdminQueueTriageNoteResponse(
                note.Id,
                note.ActorUserId,
                users.TryGetValue(note.ActorUserId, out var actorName) ? actorName : null,
                note.Note,
                note.CreatedAtUtc)).ToArray());
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        AdminQueueTriage triage,
        object? previous,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                principal.IsInRole(SwyftlyRoles.SuperAdmin) ? SwyftlyRoles.SuperAdmin : SwyftlyRoles.Admin,
                actionType,
                "AdminQueueTriage",
                triage.Id.ToString(),
                JsonSerializer.Serialize(previous),
                JsonSerializer.Serialize(CreateAuditSnapshot(triage)),
                null,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static object CreateAuditSnapshot(AdminQueueTriage triage) => new
    {
        triage.ItemType,
        triage.ItemId,
        triage.AssignedToUserId,
        triage.Priority,
        triage.LatestNote,
        triage.UpdatedAtUtc
    };

    private static bool TryParseItemType(string value, out AdminQueueItemType itemType) =>
        Enum.TryParse(value, ignoreCase: true, out itemType);

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid actorUserId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult ActorNotFound() =>
        HttpResults.Problem(
            title: "AdminQueueTriage.ActorNotFound",
            detail: "The authenticated admin user id could not be read.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult ItemNotFound() =>
        HttpResults.Problem(
            title: "AdminQueueTriage.ItemNotFound",
            detail: "The moderation queue item was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record AdminQueueItemKey(AdminQueueItemType ItemType, Guid ItemId);

public sealed record AdminQueueTriageSummaryResponse(
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    string Priority,
    string? LatestTriageNote,
    int TriageNoteCount,
    DateTimeOffset? TriageUpdatedAtUtc);

public sealed record AdminQueueTriageResponse(
    string ItemType,
    Guid ItemId,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    string Priority,
    string? LatestTriageNote,
    int TriageNoteCount,
    DateTimeOffset? TriageUpdatedAtUtc,
    IReadOnlyCollection<AdminQueueTriageNoteResponse> Notes);

public sealed record AdminQueueTriageNoteResponse(
    Guid NoteId,
    Guid ActorUserId,
    string? ActorDisplayName,
    string Note,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminQueueTriageUpdateRequest(
    string? Priority,
    string? Note,
    Guid? AssignedToUserId,
    bool? ClearAssignment);

public sealed record AdminQueueBulkTriageRequest(
    string? Action,
    string? Priority,
    string? Note,
    IReadOnlyCollection<AdminQueueBulkTriageItemRequest> Items);

public sealed record AdminQueueBulkTriageItemRequest(string ItemType, Guid ItemId);

public sealed record AdminQueueBulkTriageResponse(
    int SuccessCount,
    int ErrorCount,
    IReadOnlyCollection<AdminQueueBulkTriageItemResult> Results);

public sealed record AdminQueueBulkTriageItemResult(
    string ItemType,
    Guid ItemId,
    bool IsSuccess,
    string? Error,
    AdminQueueTriageResponse? Triage);
