using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Notifications;
using Mabuntle.Api.Sellers;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Admin;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminSellerEndpoints
{
    public static IEndpointRouteBuilder MapAdminSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/sellers")
            .WithTags("Admin Sellers")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("", GetAllAsync)
            .WithName("GetAdminSellers")
            .WithSummary("Returns seller verification records for operational admin review.")
            .Produces<AdminPagedResponse<AdminSellerOperationalSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingSellers")
            .WithSummary("Returns sellers submitted for verification review.")
            .Produces<IReadOnlyCollection<AdminSellerSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{sellerId:guid}", GetByIdAsync)
            .WithName("GetAdminSellerDetail")
            .WithSummary("Returns seller verification detail for admin review.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{sellerId:guid}/verification-evidence/{evidenceId:guid}/download", DownloadVerificationEvidenceAsync)
            .WithName("DownloadAdminSellerVerificationEvidence")
            .WithSummary("Downloads private seller verification evidence for admin review.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/approve", ApproveAsync)
            .WithName("ApproveSeller")
            .WithSummary("Approves a seller and marks the seller as verified.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/reject", RejectAsync)
            .WithName("RejectSeller")
            .WithSummary("Rejects a seller verification submission.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{sellerId:guid}/suspend", SuspendAsync)
            .WithName("SuspendSeller")
            .WithSummary("Suspends a seller.")
            .Produces<AdminSellerDetailResponse>(StatusCodes.Status200OK)
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
        MabuntleDbContext dbContext,
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

        SellerVerificationStatus? requestedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SellerVerificationStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return Validation("status", "Unknown seller verification status.");
            }

            requestedStatus = parsedStatus;
        }

        var sellers = await dbContext.SellerProfiles
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sellerIds = sellers.Select(item => item.Id).ToHashSet();
        var storefronts = await dbContext.SellerStorefronts
            .AsNoTracking()
            .Where(item => sellerIds.Contains(item.SellerId))
            .ToDictionaryAsync(item => item.SellerId, cancellationToken);
        var verificationRows = await dbContext.SellerVerifications
            .AsNoTracking()
            .Where(item => sellerIds.Contains(item.SellerId))
            .ToListAsync(cancellationToken);
        var latestVerifications = verificationRows
            .GroupBy(item => item.SellerId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.SubmittedAtUtc).First());

        var items = sellers.Select(seller =>
        {
            storefronts.TryGetValue(seller.Id, out var storefront);
            latestVerifications.TryGetValue(seller.Id, out var latestVerification);
            return new AdminSellerOperationalSummaryResponse(
                seller.Id,
                seller.DisplayName,
                seller.ContactEmail,
                storefront?.StoreName,
                storefront?.Slug,
                seller.VerificationStatus.ToString(),
                latestVerification?.SubmittedAtUtc,
                seller.UpdatedAtUtc,
                $"/admin/sellers/{seller.Id}");
        });

        if (sellerId.HasValue)
        {
            items = items.Where(item => item.SellerId == sellerId.Value);
        }

        if (requestedStatus is null && normalizedView == "NeedsAttention")
        {
            items = items.Where(item => string.Equals(item.VerificationStatus, SellerVerificationStatus.UnderReview.ToString(), StringComparison.Ordinal));
        }

        var triageItemList = items.ToList();
        var triageSummaries = await AdminQueueTriageEndpoints.GetTriageSummariesAsync(
            dbContext,
            triageItemList.Select(item => new AdminQueueItemKey(AdminQueueItemType.Seller, item.SellerId)),
            cancellationToken);
        items = triageItemList.Select(item =>
        {
            triageSummaries.TryGetValue(new AdminQueueItemKey(AdminQueueItemType.Seller, item.SellerId), out var triage);
            return item with
            {
                AssignedToUserId = triage?.AssignedToUserId,
                AssignedToDisplayName = triage?.AssignedToDisplayName,
                Priority = triage?.Priority ?? AdminQueuePriority.Normal.ToString(),
                LatestTriageNote = triage?.LatestTriageNote,
                TriageNoteCount = triage?.TriageNoteCount ?? 0,
                TriageUpdatedAtUtc = triage?.TriageUpdatedAtUtc,
                AgeHours = AdminQueueSla.Calculate(AdminQueueItemType.Seller, item.SubmittedAtUtc, item.UpdatedAtUtc, now).AgeHours,
                SlaStatus = AdminQueueSla.Calculate(AdminQueueItemType.Seller, item.SubmittedAtUtc, item.UpdatedAtUtc, now).SlaStatus,
                SlaDueAtUtc = AdminQueueSla.Calculate(AdminQueueItemType.Seller, item.SubmittedAtUtc, item.UpdatedAtUtc, now).SlaDueAtUtc
            };
        });

        items = items.Where(item => AdminQueueSla.Matches(new AdminQueueSlaResponse(item.AgeHours, item.SlaStatus, item.SlaDueAtUtc ?? DateTimeOffset.MinValue), sla));

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            items = items.Where(item => TextMatches(normalizedSearch, item.DisplayName, item.ContactEmail, item.StoreName, item.StoreSlug, item.VerificationStatus));
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
            items = statusCountSource.Where(item => string.Equals(item.VerificationStatus, requestedStatus.Value.ToString(), StringComparison.Ordinal));
        }

        items = normalizedSort.ToLowerInvariant() switch
        {
            "updatedasc" => items.OrderBy(item => item.UpdatedAtUtc),
            "submitteddesc" => items.OrderByDescending(item => item.SubmittedAtUtc ?? DateTimeOffset.MinValue),
            "submittedasc" => items.OrderBy(item => item.SubmittedAtUtc ?? DateTimeOffset.MaxValue),
            "nameasc" => items.OrderBy(item => item.StoreName ?? item.DisplayName ?? string.Empty),
            "namedesc" => items.OrderByDescending(item => item.StoreName ?? item.DisplayName ?? string.Empty),
            "statusasc" => items.OrderBy(item => item.VerificationStatus),
            "statusdesc" => items.OrderByDescending(item => item.VerificationStatus),
            _ => items.OrderByDescending(item => item.UpdatedAtUtc)
        };

        var orderedItems = items.ToList();
        var totalCount = orderedItems.Count;
        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .ToList();

        return HttpResults.Ok(new AdminPagedResponse<AdminSellerOperationalSummaryResponse>(
            pagedItems,
            totalCount,
            pageNumber,
            requestedPageSize,
            BuildStatusCounts(statusCountSource.Select(item => item.VerificationStatus))));
    }

    private static async Task<IResult> GetPendingAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sellers = await dbContext.SellerProfiles
            .Where(seller => seller.VerificationStatus == SellerVerificationStatus.UnderReview)
            .OrderBy(seller => seller.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var responses = new List<AdminSellerSummaryResponse>();

        foreach (var seller in sellers)
        {
            var storefront = await dbContext.SellerStorefronts
                .SingleOrDefaultAsync(item => item.SellerId == seller.Id, cancellationToken);
            var latestVerification = await dbContext.SellerVerifications
                .Where(item => item.SellerId == seller.Id)
                .OrderByDescending(item => item.SubmittedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            responses.Add(new AdminSellerSummaryResponse(
                seller.Id,
                seller.DisplayName,
                seller.ContactEmail,
                storefront?.StoreName,
                storefront?.Slug,
                seller.VerificationStatus.ToString(),
                latestVerification?.SubmittedAtUtc));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken);
        return detail is null ? SellerNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> DownloadVerificationEvidenceAsync(
        Guid sellerId,
        Guid evidenceId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        ISellerVerificationEvidenceStorage storage,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var evidence = await dbContext.SellerVerificationEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == evidenceId && item.SellerId == sellerId && item.RemovedAtUtc == null, cancellationToken);
        if (evidence is null)
        {
            return EvidenceNotFound();
        }

        var readFile = await storage.OpenReadAsync(
            evidence.StorageKey,
            evidence.ContentType,
            evidence.OriginalFileName,
            cancellationToken);
        if (readFile is null)
        {
            return EvidenceNotFound();
        }

        var actorUserId = GetActorUserId(principal);
        var actorRole = principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId?.ToString(),
                actorRole,
                "SellerVerificationEvidenceDownloaded",
                "SellerVerificationEvidence",
                evidence.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    evidence.SellerId,
                    evidence.EvidenceType,
                    evidence.OriginalFileName
                }),
                null,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.File(readFile.Content, readFile.ContentType, readFile.FileName);
    }

    private static async Task<IResult> ApproveAsync(
        Guid sellerId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var related = await GetRelatedAsync(sellerId, dbContext, cancellationToken);
        if (related.PayoutProfile is null)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payout"] = ["Seller payout placeholder must exist before approval."]
            });
        }

        var previousStatus = seller.VerificationStatus;
        related.PayoutProfile.MarkAdminApproved(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow());

        try
        {
            seller.MarkVerified(related.Storefront, related.Address, related.PayoutProfile);
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seller"] = [exception.Message]
            });
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerApproved",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            reason: null,
            cancellationToken);

        await UpdateLatestVerificationAsync(seller.Id, dbContext, verification =>
        {
            verification.Approve(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow());
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            seller.Id,
            SellerNotificationTypes.SellerVerificationApproved,
            "Seller verification approved",
            "Your seller account has been approved. You can now submit products for marketplace review.",
            "SellerProfile",
            seller.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminSellerEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid sellerId,
        AdminSellerReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
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

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var previousStatus = seller.VerificationStatus;
        seller.MarkRejected(request.Reason);

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerRejected",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            request.Reason,
            cancellationToken);

        await UpdateLatestVerificationAsync(seller.Id, dbContext, verification =>
        {
            verification.Reject(GetActorUserId(principal) ?? Guid.Empty, timeProvider.GetUtcNow(), request.Reason);
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            seller.Id,
            SellerNotificationTypes.SellerVerificationRejected,
            "Seller verification rejected",
            $"Your seller verification was rejected. Reason: {request.Reason.Trim()}",
            "SellerProfile",
            seller.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminSellerEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> SuspendAsync(
        Guid sellerId,
        AdminSellerReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
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

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var previousStatus = seller.VerificationStatus;
        seller.Suspend();

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "SellerSuspended",
            seller.Id,
            previousStatus,
            seller.VerificationStatus,
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            seller.Id,
            SellerNotificationTypes.SellerSuspended,
            "Seller account suspended",
            $"Your seller account has been suspended. Reason: {request.Reason.Trim()}",
            "SellerProfile",
            seller.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminSellerEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(sellerId, dbContext, cancellationToken));
    }

    private static async Task<AdminSellerDetailResponse?> CreateDetailResponseAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return null;
        }

        var related = await GetRelatedAsync(sellerId, dbContext, cancellationToken);
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "SellerProfile" && auditLog.EntityId == sellerId.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var evidence = await dbContext.SellerVerificationEvidence
            .AsNoTracking()
            .Where(item => item.SellerId == sellerId && item.RemovedAtUtc == null)
            .OrderBy(item => item.EvidenceType)
            .ThenByDescending(item => item.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        return new AdminSellerDetailResponse(
            seller.Id,
            seller.UserId,
            seller.VerificationStatus.ToString(),
            seller.DisplayName,
            seller.ContactEmail,
            seller.PhoneNumber,
            seller.BusinessType?.ToString(),
            seller.BusinessName,
            related.Storefront is null
                ? null
                : new AdminSellerStorefrontResponse(
                    related.Storefront.StoreName,
                    related.Storefront.Slug,
                    related.Storefront.Description,
                    related.Storefront.LogoUrl,
                    related.Storefront.BannerUrl,
                    related.Storefront.IsPublished),
            related.Address is null
                ? null
                : new AdminSellerAddressResponse(
                    related.Address.AddressLine1,
                    related.Address.AddressLine2,
                    related.Address.City,
                    related.Address.Province,
                    related.Address.PostalCode,
                    related.Address.CountryCode),
            related.PayoutProfile is null
                ? null
                : new AdminSellerPayoutResponse(
                    related.PayoutProfile.PayoutProviderReference,
                    related.PayoutProfile.HasSubmittedPlaceholder,
                    related.PayoutProfile.IsAdminApproved),
            SellerPolicyResponseMapper.Map(related.StorePolicy),
            evidence.Select(SellerVerificationEvidenceResponseMapper.Map).ToList(),
            auditTrail);
    }

    private static async Task<SellerRelatedData> GetRelatedAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var storefront = await dbContext.SellerStorefronts.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);
        var address = await dbContext.SellerAddresses.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);
        var payoutProfile = await dbContext.SellerPayoutProfiles.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);
        var storePolicy = await dbContext.SellerStorePolicies.SingleOrDefaultAsync(item => item.SellerId == sellerId, cancellationToken);

        return new SellerRelatedData(storefront, address, payoutProfile, storePolicy);
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        Guid sellerId,
        SellerVerificationStatus previousStatus,
        SellerVerificationStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorRole = principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                actorUserId,
                actorRole,
                actionType,
                "SellerProfile",
                sellerId.ToString(),
                JsonSerializer.Serialize(new { verificationStatus = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { verificationStatus = newStatus.ToString() }),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static async Task UpdateLatestVerificationAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        Action<SellerVerification> update,
        CancellationToken cancellationToken)
    {
        var latestVerification = await dbContext.SellerVerifications
            .Where(verification => verification.SellerId == sellerId)
            .OrderByDescending(verification => verification.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVerification is not null)
        {
            update(latestVerification);
        }
    }

    private static Guid? GetActorUserId(ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
    }

    private static IResult ReasonRequired() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reason"] = ["Reason is required."]
        });

    private static IResult Validation(string field, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
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

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "AdminSellers.SellerNotFound",
            detail: "Seller was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult EvidenceNotFound() =>
        HttpResults.Problem(
            title: "AdminSellers.VerificationEvidenceNotFound",
            detail: "Seller verification evidence was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record SellerRelatedData(
        SellerStorefront? Storefront,
        SellerAddress? Address,
        SellerPayoutProfilePlaceholder? PayoutProfile,
        SellerStorePolicy? StorePolicy);
}

public sealed record AdminSellerSummaryResponse(
    Guid SellerId,
    string? DisplayName,
    string? ContactEmail,
    string? StoreName,
    string? StoreSlug,
    string VerificationStatus,
    DateTimeOffset? SubmittedAtUtc);

public sealed record AdminSellerOperationalSummaryResponse(
    Guid SellerId,
    string? DisplayName,
    string? ContactEmail,
    string? StoreName,
    string? StoreSlug,
    string VerificationStatus,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc,
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

public sealed record AdminPagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyCollection<AdminStatusCountResponse> StatusCounts);

public sealed record AdminStatusCountResponse(string Status, int Count);

public sealed record AdminSellerDetailResponse(
    Guid SellerId,
    Guid UserId,
    string VerificationStatus,
    string? DisplayName,
    string? ContactEmail,
    string? PhoneNumber,
    string? BusinessType,
    string? BusinessName,
    AdminSellerStorefrontResponse? Storefront,
    AdminSellerAddressResponse? Address,
    AdminSellerPayoutResponse? Payout,
    SellerPolicyResponse StorePolicy,
    IReadOnlyCollection<SellerVerificationEvidenceResponse> VerificationEvidence,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminSellerStorefrontResponse(
    string StoreName,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    bool IsPublished);

public sealed record AdminSellerAddressResponse(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Province,
    string PostalCode,
    string CountryCode);

public sealed record AdminSellerPayoutResponse(
    string PayoutProviderReference,
    bool HasSubmittedPlaceholder,
    bool IsAdminApproved);

public sealed record AdminAuditLogResponse(
    Guid Id,
    string ActionType,
    string? ActorUserId,
    string? ActorRole,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminSellerReasonRequest(string Reason);
