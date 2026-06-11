using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Catalog;
using Mabuntle.Api.Notifications;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Ai;
using Mabuntle.Application.Catalog;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Search;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Admin;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminProductEndpoints
{
    public static IEndpointRouteBuilder MapAdminProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/products")
            .WithTags("Admin Products")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("/pending-review", GetPendingReviewAsync)
            .WithName("GetPendingReviewProducts")
            .WithSummary("Returns products waiting for admin marketplace review.")
            .Produces<IReadOnlyCollection<AdminProductSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/pending-revisions", GetPendingRevisionsAsync)
            .WithName("GetPendingProductListingRevisions")
            .WithSummary("Returns published product listing revisions waiting for admin review.")
            .Produces<IReadOnlyCollection<AdminProductRevisionSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/revisions/{revisionId:guid}", GetRevisionByIdAsync)
            .WithName("GetAdminProductListingRevision")
            .WithSummary("Returns a published product listing revision for admin review.")
            .Produces<AdminProductRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/revisions/{revisionId:guid}/approve", ApproveRevisionAsync)
            .WithName("ApproveProductListingRevision")
            .WithSummary("Approves a published product listing revision and applies it to the live product.")
            .Produces<AdminProductRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/revisions/{revisionId:guid}/reject", RejectRevisionAsync)
            .WithName("RejectProductListingRevision")
            .WithSummary("Rejects a published product listing revision without changing the live product.")
            .Produces<AdminProductRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/pending-variant-revisions", GetPendingVariantRevisionsAsync)
            .WithName("GetPendingProductVariantRevisions")
            .WithSummary("Returns published product variant and pricing revisions waiting for admin review.")
            .Produces<IReadOnlyCollection<AdminProductVariantRevisionSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/moderation-items", GetModerationItemsAsync)
            .WithName("GetAdminProductModerationItems")
            .WithSummary("Returns products and published-product revisions for all-state admin moderation.")
            .Produces<AdminPagedResponse<AdminProductModerationItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/variant-revisions/{revisionId:guid}", GetVariantRevisionByIdAsync)
            .WithName("GetAdminProductVariantRevision")
            .WithSummary("Returns a published product variant and pricing revision for admin review.")
            .Produces<AdminProductVariantRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/variant-revisions/{revisionId:guid}/approve", ApproveVariantRevisionAsync)
            .WithName("ApproveProductVariantRevision")
            .WithSummary("Approves a published product variant and pricing revision and applies it to the live variants.")
            .Produces<AdminProductVariantRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/variant-revisions/{revisionId:guid}/reject", RejectVariantRevisionAsync)
            .WithName("RejectProductVariantRevision")
            .WithSummary("Rejects a published product variant and pricing revision without changing live variants.")
            .Produces<AdminProductVariantRevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{productId:guid}", GetByIdAsync)
            .WithName("GetAdminProductDetail")
            .WithSummary("Returns product detail for admin marketplace review.")
            .Produces<AdminProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{productId:guid}/approve", ApproveAsync)
            .WithName("ApproveProduct")
            .WithSummary("Approves a product and publishes it when the seller is verified.")
            .Produces<AdminProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{productId:guid}/reject", RejectAsync)
            .WithName("RejectProduct")
            .WithSummary("Rejects a product review submission.")
            .Produces<AdminProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{productId:guid}/request-changes", RequestChangesAsync)
            .WithName("RequestProductChanges")
            .WithSummary("Requests seller changes for a product review submission.")
            .Produces<AdminProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetModerationItemsAsync(
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
        var normalizedStatus = status?.Trim();
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "UpdatedDesc" : sort.Trim();
        var now = timeProvider.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(normalizedStatus) && !IsKnownProductModerationStatus(normalizedStatus))
        {
            return Validation("status", "Unknown product moderation status.");
        }

        var sellerProfiles = await dbContext.SellerProfiles
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var items = new List<AdminProductModerationItemResponse>();

        var products = await dbContext.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            if (sellerId.HasValue && product.SellerId != sellerId.Value)
            {
                continue;
            }

            sellerProfiles.TryGetValue(product.SellerId, out var seller);
            var highRiskCount = await dbContext.AiModerationResults.CountAsync(
                result => result.ProductId == product.Id
                    && result.NeedsAdminReview
                    && result.RiskLevel == AiModerationRiskLevel.High,
                cancellationToken);
            var variantCount = await dbContext.ProductVariants.CountAsync(
                variant => variant.ProductId == product.Id,
                cancellationToken);

            items.Add(new AdminProductModerationItemResponse(
                product.Id,
                "Product",
                product.Id,
                null,
                product.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                product.Title,
                await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
                product.Status.ToString(),
                product.Status is ProductStatus.PendingReview or ProductStatus.NeedsAdminReview ? product.UpdatedAtUtc : null,
                product.UpdatedAtUtc,
                highRiskCount,
                variantCount,
                $"/admin/products/{product.Id}"));
        }

        var listingRevisions = await dbContext.ProductListingRevisions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        foreach (var revision in listingRevisions)
        {
            if (sellerId.HasValue && revision.SellerId != sellerId.Value)
            {
                continue;
            }

            sellerProfiles.TryGetValue(revision.SellerId, out var seller);
            var product = products.FirstOrDefault(item => item.Id == revision.ProductId);
            items.Add(new AdminProductModerationItemResponse(
                revision.Id,
                "ListingRevision",
                revision.ProductId,
                revision.Id,
                revision.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                revision.Title ?? product?.Title,
                await GetCategoryPathAsync(revision.CategoryId ?? product?.CategoryId, dbContext, cancellationToken),
                revision.Status.ToString(),
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc,
                0,
                await dbContext.ProductListingRevisionImages.CountAsync(image => image.RevisionId == revision.Id, cancellationToken),
                $"/admin/products/revisions/{revision.Id}"));
        }

        var variantRevisions = await dbContext.ProductVariantRevisions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        foreach (var revision in variantRevisions)
        {
            if (sellerId.HasValue && revision.SellerId != sellerId.Value)
            {
                continue;
            }

            sellerProfiles.TryGetValue(revision.SellerId, out var seller);
            var product = products.FirstOrDefault(item => item.Id == revision.ProductId);
            items.Add(new AdminProductModerationItemResponse(
                revision.Id,
                "VariantRevision",
                revision.ProductId,
                revision.Id,
                revision.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                product?.Title,
                await GetCategoryPathAsync(product?.CategoryId, dbContext, cancellationToken),
                revision.Status.ToString(),
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc,
                0,
                await dbContext.ProductVariantRevisionItems.CountAsync(item => item.RevisionId == revision.Id, cancellationToken),
                $"/admin/products/variant-revisions/{revision.Id}"));
        }

        var triageSummaries = await AdminQueueTriageEndpoints.GetTriageSummariesAsync(
            dbContext,
            items.Select(item => new AdminQueueItemKey(ParseModerationItemType(item.ItemType), item.Id)),
            cancellationToken);
        IEnumerable<AdminProductModerationItemResponse> filteredItems = items.Select(item =>
        {
            triageSummaries.TryGetValue(new AdminQueueItemKey(ParseModerationItemType(item.ItemType), item.Id), out var triage);
            var slaState = AdminQueueSla.Calculate(ParseModerationItemType(item.ItemType), item.SubmittedAtUtc, item.UpdatedAtUtc, now);
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
        filteredItems = filteredItems.Where(item => AdminQueueSla.Matches(new AdminQueueSlaResponse(item.AgeHours, item.SlaStatus, item.SlaDueAtUtc ?? DateTimeOffset.MinValue), sla));
        if (string.IsNullOrWhiteSpace(normalizedStatus) && normalizedView == "NeedsAttention")
        {
            filteredItems = filteredItems.Where(item =>
                (item.ItemType == "Product" && (item.Status == ProductStatus.PendingReview.ToString() || item.Status == ProductStatus.NeedsAdminReview.ToString()))
                || (item.ItemType == "ListingRevision" && item.Status == ProductListingRevisionStatus.PendingReview.ToString())
                || (item.ItemType == "VariantRevision" && item.Status == ProductVariantRevisionStatus.PendingReview.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredItems = filteredItems.Where(item => TextMatches(
                normalizedSearch,
                item.ItemType,
                item.Title,
                item.CategoryPath,
                item.SellerDisplayName,
                item.SellerVerificationStatus,
                item.Status));
        }

        filteredItems = filteredItems.Where(item => AdminQueueTriageEndpoints.MatchesTriageFilters(
            new AdminQueueTriageSummaryResponse(item.AssignedToUserId, item.AssignedToDisplayName, item.Priority, item.LatestTriageNote, item.TriageNoteCount, item.TriageUpdatedAtUtc),
            assigned,
            priority,
            hasNotes,
            principal));

        var statusCountSource = filteredItems.ToList();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            filteredItems = statusCountSource.Where(item => string.Equals(item.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
        }

        filteredItems = normalizedSort.ToLowerInvariant() switch
        {
            "updatedasc" => filteredItems.OrderBy(item => item.UpdatedAtUtc),
            "submitteddesc" => filteredItems.OrderByDescending(item => item.SubmittedAtUtc ?? DateTimeOffset.MinValue),
            "submittedasc" => filteredItems.OrderBy(item => item.SubmittedAtUtc ?? DateTimeOffset.MaxValue),
            "nameasc" => filteredItems.OrderBy(item => item.Title ?? string.Empty),
            "namedesc" => filteredItems.OrderByDescending(item => item.Title ?? string.Empty),
            "statusasc" => filteredItems.OrderBy(item => item.Status),
            "statusdesc" => filteredItems.OrderByDescending(item => item.Status),
            "typeasc" => filteredItems.OrderBy(item => item.ItemType),
            "typedesc" => filteredItems.OrderByDescending(item => item.ItemType),
            _ => filteredItems.OrderByDescending(item => item.UpdatedAtUtc)
        };

        var orderedItems = filteredItems.ToList();
        var totalCount = orderedItems.Count;
        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .ToList();

        return HttpResults.Ok(new AdminPagedResponse<AdminProductModerationItemResponse>(
            pagedItems,
            totalCount,
            pageNumber,
            requestedPageSize,
            BuildStatusCounts(statusCountSource.Select(item => item.Status))));
    }

    private static async Task<IResult> GetPendingReviewAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .Where(product => product.Status == ProductStatus.PendingReview || product.Status == ProductStatus.NeedsAdminReview)
            .OrderBy(product => product.UpdatedAtUtc)
            .Select(product => new
            {
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Title,
                product.Status,
                product.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var responses = new List<AdminProductSummaryResponse>();
        foreach (var product in products)
        {
            var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
                seller => seller.Id == product.SellerId,
                cancellationToken);
            var highRiskCount = await dbContext.AiModerationResults.CountAsync(
                result => result.ProductId == product.Id
                    && result.NeedsAdminReview
                    && result.RiskLevel == AiModerationRiskLevel.High,
                cancellationToken);

            responses.Add(new AdminProductSummaryResponse(
                product.Id,
                product.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                product.Title,
                await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
                product.Status.ToString(),
                highRiskCount,
                product.UpdatedAtUtc));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetPendingRevisionsAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revisions = await dbContext.ProductListingRevisions
            .Where(revision => revision.Status == ProductListingRevisionStatus.PendingReview)
            .OrderBy(revision => revision.SubmittedAtUtc)
            .Select(revision => new
            {
                revision.Id,
                revision.ProductId,
                revision.SellerId,
                revision.Title,
                revision.Status,
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var responses = new List<AdminProductRevisionSummaryResponse>();
        foreach (var revision in revisions)
        {
            var product = await dbContext.Products.SingleOrDefaultAsync(
                product => product.Id == revision.ProductId,
                cancellationToken);
            var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
                seller => seller.Id == revision.SellerId,
                cancellationToken);

            responses.Add(new AdminProductRevisionSummaryResponse(
                revision.Id,
                revision.ProductId,
                revision.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                product?.Title,
                revision.Title,
                revision.Status.ToString(),
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetRevisionByIdAsync(
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateRevisionDetailResponseAsync(revisionId, dbContext, cancellationToken);
        return detail is null ? ProductNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveRevisionAsync(
        Guid revisionId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        IProductSearchIndexer productSearchIndexer,
        IProductEmbeddingGenerator productEmbeddingGenerator,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductListingRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return ProductNotFound();
        }

        if (revision.Status != ProductListingRevisionStatus.PendingReview)
        {
            return Validation("revision", "Only pending revisions can be approved.");
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == revision.ProductId,
            cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == product.SellerId, cancellationToken);
        if (seller?.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return Validation("seller", "Only products from verified sellers can apply approved revisions.");
        }

        var validation = await ValidateRevisionForApprovalAsync(revision, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        if (!TryGetUserId(principal, out var reviewedByUserId))
        {
            return Validation("user", "Authenticated user id is required.");
        }

        var previousValue = JsonSerializer.Serialize(new
        {
            product.Title,
            product.Slug,
            product.CategoryId,
            product.BrandId,
            product.ShortDescription,
            product.FullDescription,
            product.TagsJson,
            product.SeoTitle,
            product.SeoDescription,
            product.MerchandisingLabel,
            product.CareInstructions,
            product.ProductDisclaimer,
            Attributes = await ReadProductAttributesForAuditAsync(product.Id, dbContext, cancellationToken)
        });

        var revisionAttributes = ParseAttributesJson(revision.AttributesJson);
        try
        {
            product.ApplyApprovedListingRevision(
                revision.CategoryId,
                revision.BrandId,
                revision.Title,
                revision.Slug,
                revision.ShortDescription,
                revision.FullDescription,
                revision.TagsJson,
                revision.SeoTitle,
                revision.SeoDescription,
                revision.MerchandisingLabel,
                revision.CareInstructions,
                revision.ProductDisclaimer);
            revision.Approve(reviewedByUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("revision", exception.Message);
        }

        await ReplaceProductAttributesAsync(product.Id, revisionAttributes, dbContext, cancellationToken);
        await ReplaceProductImagesAsync(product.Id, revision.Id, dbContext, cancellationToken);
        await AddRevisionAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductListingRevisionApproved",
            revision,
            previousValue,
            JsonSerializer.Serialize(new
            {
                revision.Title,
                revision.Slug,
                revision.CategoryId,
                revision.BrandId,
                revision.ShortDescription,
                revision.FullDescription,
                revision.TagsJson,
                revision.SeoTitle,
                revision.SeoDescription,
                revision.MerchandisingLabel,
                revision.CareInstructions,
                revision.ProductDisclaimer,
                revision.AttributesJson
            }),
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await productSearchIndexer.IndexProductAsync(product.Id, cancellationToken);
        await productEmbeddingGenerator.GenerateForProductAsync(product.Id, cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            revision.SellerId,
            SellerNotificationTypes.ProductListingRevisionApproved,
            "Published listing revision approved",
            $"Your published listing revision for {product.Title ?? "a product"} was approved and is now live.",
            "Product",
            product.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectRevisionAsync(
        Guid revisionId,
        AdminProductReasonRequest request,
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

        var revision = await dbContext.ProductListingRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return ProductNotFound();
        }

        if (!TryGetUserId(principal, out var reviewedByUserId))
        {
            return Validation("user", "Authenticated user id is required.");
        }

        var previousStatus = revision.Status;
        try
        {
            revision.Reject(request.Reason, reviewedByUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("revision", exception.Message);
        }

        await AddRevisionAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductListingRevisionRejected",
            revision,
            JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { status = revision.Status.ToString(), reason = revision.RejectionReason }),
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            revision.SellerId,
            SellerNotificationTypes.ProductListingRevisionRejected,
            "Published listing revision rejected",
            $"Your published listing revision for {revision.Title ?? "a product"} was rejected. Reason: {request.Reason.Trim()}",
            "Product",
            revision.ProductId,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetPendingVariantRevisionsAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revisions = await dbContext.ProductVariantRevisions
            .Where(revision => revision.Status == ProductVariantRevisionStatus.PendingReview)
            .OrderBy(revision => revision.SubmittedAtUtc)
            .Select(revision => new
            {
                revision.Id,
                revision.ProductId,
                revision.SellerId,
                revision.Status,
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var responses = new List<AdminProductVariantRevisionSummaryResponse>();
        foreach (var revision in revisions)
        {
            var product = await dbContext.Products.SingleOrDefaultAsync(
                product => product.Id == revision.ProductId,
                cancellationToken);
            var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
                seller => seller.Id == revision.SellerId,
                cancellationToken);
            var itemCount = await dbContext.ProductVariantRevisionItems.CountAsync(
                item => item.RevisionId == revision.Id,
                cancellationToken);

            responses.Add(new AdminProductVariantRevisionSummaryResponse(
                revision.Id,
                revision.ProductId,
                revision.SellerId,
                seller?.DisplayName,
                seller?.VerificationStatus.ToString(),
                product?.Title,
                revision.Status.ToString(),
                itemCount,
                revision.SubmittedAtUtc,
                revision.UpdatedAtUtc));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetVariantRevisionByIdAsync(
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateVariantRevisionDetailResponseAsync(revisionId, dbContext, cancellationToken);
        return detail is null ? ProductNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveVariantRevisionAsync(
        Guid revisionId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        IProductSearchIndexer productSearchIndexer,
        IProductEmbeddingGenerator productEmbeddingGenerator,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductVariantRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return ProductNotFound();
        }

        if (revision.Status != ProductVariantRevisionStatus.PendingReview)
        {
            return Validation("revision", "Only pending variant revisions can be approved.");
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == revision.ProductId,
            cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Variant revisions can be approved only for published products.");
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == product.SellerId, cancellationToken);
        if (seller?.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return Validation("seller", "Only products from verified sellers can apply approved variant revisions.");
        }

        if (!TryGetUserId(principal, out var reviewedByUserId))
        {
            return Validation("user", "Authenticated user id is required.");
        }

        var items = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        var validation = await ProductVariantRevisionRules.ValidateAsync(product.Id, items, dbContext, cancellationToken);
        if (!validation.IsValid)
        {
            return HttpResults.ValidationProblem(validation.Errors);
        }

        var previousValue = JsonSerializer.Serialize(new
        {
            Variants = await ReadProductVariantsForAuditAsync(product.Id, dbContext, cancellationToken)
        });

        try
        {
            await ApplyVariantRevisionAsync(product.Id, items, dbContext, cancellationToken);
            revision.Approve(reviewedByUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("revision", exception.Message);
        }

        await AddVariantRevisionAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductVariantRevisionApproved",
            revision,
            previousValue,
            JsonSerializer.Serialize(new
            {
                FinalVariants = validation.FinalVariants.Select(variant => new
                {
                    variant.SourceVariantId,
                    variant.ChangeType,
                    variant.Sku,
                    variant.Size,
                    variant.Colour,
                    variant.Price,
                    variant.CompareAtPrice,
                    Status = variant.Status.ToString()
                })
            }),
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await productSearchIndexer.IndexProductAsync(product.Id, cancellationToken);
        await productEmbeddingGenerator.GenerateForProductAsync(product.Id, cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            revision.SellerId,
            SellerNotificationTypes.ProductVariantRevisionApproved,
            "Variant and pricing revision approved",
            $"Your variant and pricing revision for {product.Title ?? "a product"} was approved and is now live.",
            "Product",
            product.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateVariantRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectVariantRevisionAsync(
        Guid revisionId,
        AdminProductReasonRequest request,
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

        var revision = await dbContext.ProductVariantRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return ProductNotFound();
        }

        if (!TryGetUserId(principal, out var reviewedByUserId))
        {
            return Validation("user", "Authenticated user id is required.");
        }

        var previousStatus = revision.Status;
        try
        {
            revision.Reject(request.Reason, reviewedByUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("revision", exception.Message);
        }

        await AddVariantRevisionAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductVariantRevisionRejected",
            revision,
            JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { status = revision.Status.ToString(), reason = revision.RejectionReason }),
            request.Reason,
            cancellationToken);

        var productTitle = await dbContext.Products
            .Where(product => product.Id == revision.ProductId)
            .Select(product => product.Title)
            .SingleOrDefaultAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            revision.SellerId,
            SellerNotificationTypes.ProductVariantRevisionRejected,
            "Variant and pricing revision rejected",
            $"Your variant and pricing revision for {productTitle ?? "a product"} was rejected. Reason: {request.Reason.Trim()}",
            "Product",
            revision.ProductId,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateVariantRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateDetailResponseAsync(productId, dbContext, cancellationToken);
        return detail is null ? ProductNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveAsync(
        Guid productId,
        AdminProductApproveRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        IProductSearchIndexer productSearchIndexer,
        IProductEmbeddingGenerator productEmbeddingGenerator,
        INotificationService notificationService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.Id == product.SellerId, cancellationToken);
        if (seller?.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return Validation("seller", "Only products from verified sellers can be approved.");
        }

        var hasHighRiskModeration = await HasHighRiskModerationAsync(product.Id, dbContext, cancellationToken);
        if (hasHighRiskModeration && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            return Validation("overrideReason", "High-risk moderation flags require an override reason before approval.");
        }

        var previousStatus = product.Status;
        try
        {
            product.Publish(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Validation("product", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductApproved",
            product.Id,
            previousStatus,
            product.Status,
            string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await productSearchIndexer.IndexProductAsync(product.Id, cancellationToken);
        await productEmbeddingGenerator.GenerateForProductAsync(product.Id, cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            product.SellerId,
            SellerNotificationTypes.ProductApproved,
            "Product approved",
            $"Your product {product.Title ?? "listing"} has been approved and published.",
            "Product",
            product.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid productId,
        AdminProductReasonRequest request,
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

        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var previousStatus = product.Status;
        try
        {
            product.Reject(request.Reason);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("product", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductRejected",
            product.Id,
            previousStatus,
            product.Status,
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            product.SellerId,
            SellerNotificationTypes.ProductRejected,
            "Product rejected",
            $"Your product {product.Title ?? "listing"} was rejected. Reason: {request.Reason.Trim()}",
            "Product",
            product.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RequestChangesAsync(
        Guid productId,
        AdminProductReasonRequest request,
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

        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var previousStatus = product.Status;
        try
        {
            product.RequestChanges(request.Reason);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("product", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductChangesRequested",
            product.Id,
            previousStatus,
            product.Status,
            request.Reason,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SellerNotificationDispatcher.NotifySellerAsync(
            product.SellerId,
            SellerNotificationTypes.ProductChangesRequested,
            "Product changes requested",
            $"Changes were requested for {product.Title ?? "your product listing"}. Reason: {request.Reason.Trim()}",
            "Product",
            product.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminProductEndpoints)),
            cancellationToken);
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<AdminProductDetailResponse?> CreateDetailResponseAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
            seller => seller.Id == product.SellerId,
            cancellationToken);
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new AdminProductVariantResponse(
                variant.Id,
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity,
                variant.ReservedQuantity,
                variant.Status.ToString(),
                variant.AvailableQuantity))
            .ToListAsync(cancellationToken);
        var images = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new AdminProductImageResponse(
                image.Id,
                image.Url,
                image.AltText,
                image.SortOrder,
                image.IsPrimary,
                image.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var attributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .OrderBy(attribute => attribute.Key)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => attribute.ValueJson,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var moderationResults = await dbContext.AiModerationResults
            .Where(result => result.ProductId == product.Id)
            .OrderByDescending(result => result.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var moderation = moderationResults
            .Select(result => new AdminProductModerationResultResponse(
                result.Id,
                result.RiskLevel.ToString(),
                result.NeedsAdminReview,
                result.Reason,
                ReadStringArray(result.DetectedTermsJson),
                ReadStringArray(result.MissingFieldsJson),
                ReadStringArray(result.FlagsJson),
                result.Provider,
                result.CreatedAtUtc))
            .ToArray();
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "Product" && auditLog.EntityId == product.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminProductDetailResponse(
            product.Id,
            product.SellerId,
            new AdminProductSellerResponse(
                seller?.DisplayName,
                seller?.ContactEmail,
                seller?.VerificationStatus.ToString()),
            product.CategoryId,
            await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
            product.BrandId,
            product.Title,
            product.Slug,
            product.ShortDescription,
            product.FullDescription,
            product.SeoTitle,
            product.SeoDescription,
            product.MerchandisingLabel,
            product.CareInstructions,
            product.ProductDisclaimer,
            ReadStringArray(product.TagsJson),
            product.Status.ToString(),
            product.RejectionReason,
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            product.PublishedAtUtc,
            attributes,
            variants,
            images,
            moderation,
            auditTrail);
    }

    private static async Task<AdminProductRevisionDetailResponse?> CreateRevisionDetailResponseAsync(
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductListingRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return null;
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == revision.ProductId,
            cancellationToken);
        if (product is null)
        {
            return null;
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
            seller => seller.Id == revision.SellerId,
            cancellationToken);
        var currentImages = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new AdminProductRevisionImageResponse(
                image.Id,
                image.Url,
                image.AltText,
                image.SortOrder,
                image.IsPrimary,
                image.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var proposedImages = await dbContext.ProductListingRevisionImages
            .Where(image => image.RevisionId == revision.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new AdminProductRevisionImageResponse(
                image.Id,
                image.Url,
                image.AltText,
                image.SortOrder,
                image.IsPrimary,
                image.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var currentAttributes = await ReadProductAttributesForAuditAsync(product.Id, dbContext, cancellationToken);
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "ProductListingRevision" && auditLog.EntityId == revision.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminProductRevisionDetailResponse(
            revision.Id,
            revision.ProductId,
            revision.SellerId,
            new AdminProductSellerResponse(
                seller?.DisplayName,
                seller?.ContactEmail,
                seller?.VerificationStatus.ToString()),
            revision.Status.ToString(),
            revision.RejectionReason,
            revision.SubmittedAtUtc,
            revision.ReviewedAtUtc,
            new AdminProductListingSnapshotResponse(
                product.CategoryId,
                await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
                product.BrandId,
                product.Title,
                product.Slug,
                product.ShortDescription,
                product.FullDescription,
                product.SeoTitle,
                product.SeoDescription,
                product.MerchandisingLabel,
                product.CareInstructions,
                product.ProductDisclaimer,
                ReadStringArray(product.TagsJson),
                currentAttributes,
                currentImages),
            new AdminProductListingSnapshotResponse(
                revision.CategoryId,
                await GetCategoryPathAsync(revision.CategoryId, dbContext, cancellationToken),
                revision.BrandId,
                revision.Title,
                revision.Slug,
                revision.ShortDescription,
                revision.FullDescription,
                revision.SeoTitle,
                revision.SeoDescription,
                revision.MerchandisingLabel,
                revision.CareInstructions,
                revision.ProductDisclaimer,
                ReadStringArray(revision.TagsJson),
                ReadRevisionAttributes(revision.AttributesJson),
                proposedImages),
            auditTrail);
    }

    private static async Task<AdminProductVariantRevisionDetailResponse?> CreateVariantRevisionDetailResponseAsync(
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductVariantRevisions.SingleOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);
        if (revision is null)
        {
            return null;
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == revision.ProductId,
            cancellationToken);
        if (product is null)
        {
            return null;
        }

        var seller = await dbContext.SellerProfiles.SingleOrDefaultAsync(
            seller => seller.Id == revision.SellerId,
            cancellationToken);
        var currentVariants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new AdminProductVariantRevisionFinalVariantResponse(
                variant.Id,
                "Live",
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity,
                variant.ReservedQuantity,
                variant.Status.ToString(),
                variant.Barcode,
                variant.AvailableQuantity))
            .ToListAsync(cancellationToken);
        var rawItems = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        var stagedItems = rawItems
            .OrderBy(item => item.Operation)
            .ThenBy(item => item.Size)
            .ThenBy(item => item.Colour)
            .Select(item => new AdminProductVariantRevisionItemResponse(
                item.Id,
                item.Operation.ToString(),
                item.SourceVariantId,
                item.Sku,
                item.Size,
                item.Colour,
                item.Price,
                item.CompareAtPrice,
                item.InitialStockQuantity,
                item.ProposedStatus.ToString(),
                item.Barcode))
            .ToArray();
        var validation = await ProductVariantRevisionRules.ValidateAsync(
            revision.ProductId,
            rawItems,
            dbContext,
            cancellationToken);
        var proposedFinalVariants = validation.FinalVariants
            .Select(variant => new AdminProductVariantRevisionFinalVariantResponse(
                variant.SourceVariantId,
                variant.ChangeType,
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity,
                variant.ReservedQuantity,
                variant.Status.ToString(),
                variant.Barcode,
                variant.AvailableQuantity))
            .ToArray();
        var auditTrail = await dbContext.AuditLogs
            .Where(auditLog => auditLog.EntityType == "ProductVariantRevision" && auditLog.EntityId == revision.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminProductVariantRevisionDetailResponse(
            revision.Id,
            revision.ProductId,
            revision.SellerId,
            new AdminProductSellerResponse(
                seller?.DisplayName,
                seller?.ContactEmail,
                seller?.VerificationStatus.ToString()),
            product.Title,
            product.Slug,
            revision.Status.ToString(),
            revision.SellerReason,
            revision.RejectionReason,
            revision.SubmittedAtUtc,
            revision.ReviewedAtUtc,
            currentVariants,
            stagedItems,
            proposedFinalVariants,
            validation.Errors,
            auditTrail);
    }

    private static async Task<IReadOnlyCollection<object>> ReadProductVariantsForAuditAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == productId)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new
            {
                variant.Id,
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity,
                variant.ReservedQuantity,
                Status = variant.Status.ToString(),
                variant.Barcode
            })
            .ToListAsync(cancellationToken);

        return variants.Cast<object>().ToArray();
    }

    private static async Task ApplyVariantRevisionAsync(
        Guid productId,
        IReadOnlyCollection<ProductVariantRevisionItem> items,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == productId)
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);

        foreach (var item in items)
        {
            if (item.Operation == ProductVariantRevisionItemOperation.Add)
            {
                dbContext.ProductVariants.Add(new ProductVariant(
                    productId,
                    item.Sku,
                    item.Size,
                    item.Colour,
                    item.Price,
                    item.CompareAtPrice,
                    item.InitialStockQuantity ?? 0,
                    reservedQuantity: 0,
                    ProductVariantStatus.Active,
                    item.Barcode));
                continue;
            }

            if (!item.SourceVariantId.HasValue || !variants.TryGetValue(item.SourceVariantId.Value, out var variant))
            {
                throw new InvalidOperationException("A staged source variant could not be found.");
            }

            if (item.Operation == ProductVariantRevisionItemOperation.Deactivate)
            {
                variant.Deactivate();
                continue;
            }

            variant.Update(
                item.Sku,
                item.Size,
                item.Colour,
                item.Price,
                item.CompareAtPrice,
                variant.StockQuantity,
                variant.ReservedQuantity,
                variant.Status,
                item.Barcode);
            await RefreshActiveCartVariantSnapshotsAsync(variant, dbContext, cancellationToken);
        }
    }

    private static async Task RefreshActiveCartVariantSnapshotsAsync(
        ProductVariant variant,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var activeCartItems = await dbContext.CartItems
            .Where(item => item.ProductVariantId == variant.Id)
            .Join(
                dbContext.Carts.Where(cart => cart.Status == Domain.Carts.CartStatus.Active),
                item => item.CartId,
                cart => cart.Id,
                (item, _) => item)
            .ToListAsync(cancellationToken);

        foreach (var item in activeCartItems)
        {
            item.RefreshVariantSnapshot(variant.Sku, variant.Size, variant.Colour, variant.Price);
        }
    }

    private static async Task<IResult?> ValidateRevisionForApprovalAsync(
        ProductListingRevision revision,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!revision.CategoryId.HasValue)
        {
            return Validation("categoryId", "Category is required.");
        }

        var categoryExists = await dbContext.Categories.AnyAsync(
            category => category.Id == revision.CategoryId && category.IsActive,
            cancellationToken);
        if (!categoryExists)
        {
            return Validation("categoryId", "Category does not exist or is inactive.");
        }

        var hasImages = await dbContext.ProductListingRevisionImages.AnyAsync(
            image => image.RevisionId == revision.Id,
            cancellationToken);
        if (!hasImages)
        {
            return Validation("images", "At least one image is required.");
        }

        var attributes = ParseAttributesJson(revision.AttributesJson);
        var definitions = await dbContext.CategoryAttributes
            .Where(attribute => attribute.CategoryId == revision.CategoryId && attribute.IsActive)
            .ToListAsync(cancellationToken);
        var values = attributes.ToDictionary(
            attribute => attribute.Key,
            attribute => ToValidationValue(attribute.Value),
            StringComparer.OrdinalIgnoreCase);
        var validation = CategoryAttributeValidator.Validate(revision.CategoryId.Value, definitions, values);

        return validation.IsValid
            ? null
            : HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["attributes"] = validation.Errors.ToArray()
            });
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadProductAttributesForAuditAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .OrderBy(attribute => attribute.Key)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => attribute.ValueJson,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

    private static IReadOnlyDictionary<string, string> ReadRevisionAttributes(string attributesJson)
    {
        using var document = JsonDocument.Parse(attributesJson);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement
                .EnumerateObject()
                .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                .ToDictionary(
                    property => property.Name.Trim().ToLowerInvariant(),
                    property => property.Value.GetRawText(),
                    StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>();
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseAttributesJson(string attributesJson)
    {
        using var document = JsonDocument.Parse(attributesJson);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement
                .EnumerateObject()
                .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                .ToDictionary(
                    property => property.Name.Trim().ToLowerInvariant(),
                    property => property.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>();
    }

    private static async Task ReplaceProductAttributesAsync(
        Guid productId,
        IReadOnlyDictionary<string, JsonElement> attributes,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingAttributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .ToListAsync(cancellationToken);
        dbContext.ProductAttributeValues.RemoveRange(existingAttributes);

        foreach (var attribute in attributes.Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key)))
        {
            dbContext.ProductAttributeValues.Add(new ProductAttributeValue(
                productId,
                attribute.Key.Trim().ToLowerInvariant(),
                attribute.Value.GetRawText()));
        }
    }

    private static async Task ReplaceProductImagesAsync(
        Guid productId,
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingImages = await dbContext.ProductImages
            .Where(image => image.ProductId == productId)
            .ToListAsync(cancellationToken);
        var revisionImages = await dbContext.ProductListingRevisionImages
            .Where(image => image.RevisionId == revisionId)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ToListAsync(cancellationToken);

        dbContext.ProductImages.RemoveRange(existingImages);
        foreach (var revisionImage in revisionImages)
        {
            dbContext.ProductImages.Add(new ProductImage(
                productId,
                revisionImage.Url,
                revisionImage.StorageKey,
                revisionImage.AltText,
                revisionImage.SortOrder,
                revisionImage.IsPrimary,
                revisionImage.CreatedAtUtc,
                revisionImage.MediaAssetId));
        }
    }

    private static async Task<bool> HasHighRiskModerationAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.AiModerationResults.AnyAsync(
            result => result.ProductId == productId
                && result.NeedsAdminReview
                && result.RiskLevel == AiModerationRiskLevel.High,
            cancellationToken);

    private static async Task<string?> GetCategoryPathAsync(
        Guid? categoryId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var categories = await dbContext.Categories.ToListAsync(cancellationToken);
        var byId = categories.ToDictionary(category => category.Id);
        var names = new Stack<string>();
        var currentId = categoryId;

        while (currentId.HasValue && byId.TryGetValue(currentId.Value, out var category))
        {
            names.Push(category.Name);
            currentId = category.ParentCategoryId;
        }

        return names.Count == 0 ? null : string.Join(" > ", names);
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        Guid productId,
        ProductStatus previousStatus,
        ProductStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorRole = principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                actorRole,
                actionType,
                "Product",
                productId.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { status = newStatus.ToString() }),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static async Task AddRevisionAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        ProductListingRevision revision,
        string? previousValue,
        string? newValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorRole = principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                actorRole,
                actionType,
                "ProductListingRevision",
                revision.Id.ToString(),
                previousValue,
                newValue,
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static async Task AddVariantRevisionAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        ProductVariantRevision revision,
        string? previousValue,
        string? newValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorRole = principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                actorRole,
                actionType,
                "ProductVariantRevision",
                revision.Id.ToString(),
                previousValue,
                newValue,
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    private static object? ToValidationValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray(),
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static IReadOnlyCollection<string> ReadStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IResult ReasonRequired() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reason"] = ["Reason is required."]
        });

    private static bool IsKnownProductModerationStatus(string status) =>
        Enum.TryParse<ProductStatus>(status, ignoreCase: true, out _)
        || Enum.TryParse<ProductListingRevisionStatus>(status, ignoreCase: true, out _)
        || Enum.TryParse<ProductVariantRevisionStatus>(status, ignoreCase: true, out _);

    private static AdminQueueItemType ParseModerationItemType(string itemType) =>
        itemType switch
        {
            "ListingRevision" => AdminQueueItemType.ListingRevision,
            "VariantRevision" => AdminQueueItemType.VariantRevision,
            _ => AdminQueueItemType.Product
        };

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

    private static IResult ProductNotFound() =>
        HttpResults.Problem(
            title: "AdminProducts.ProductNotFound",
            detail: "Product was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record AdminProductSummaryResponse(
    Guid ProductId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerVerificationStatus,
    string? Title,
    string? CategoryPath,
    string Status,
    int HighRiskFlagCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminProductRevisionSummaryResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerVerificationStatus,
    string? CurrentTitle,
    string? ProposedTitle,
    string Status,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminProductVariantRevisionSummaryResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerVerificationStatus,
    string? ProductTitle,
    string Status,
    int ItemCount,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminProductModerationItemResponse(
    Guid Id,
    string ItemType,
    Guid ProductId,
    Guid? RevisionId,
    Guid SellerId,
    string? SellerDisplayName,
    string? SellerVerificationStatus,
    string? Title,
    string? CategoryPath,
    string Status,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int RiskFlagCount,
    int ItemCount,
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

public sealed record AdminProductDetailResponse(
    Guid ProductId,
    Guid SellerId,
    AdminProductSellerResponse Seller,
    Guid? CategoryId,
    string? CategoryPath,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    string? SeoTitle,
    string? SeoDescription,
    string? MerchandisingLabel,
    string? CareInstructions,
    string? ProductDisclaimer,
    IReadOnlyCollection<string> Tags,
    string Status,
    string? RejectionReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<AdminProductVariantResponse> Variants,
    IReadOnlyCollection<AdminProductImageResponse> Images,
    IReadOnlyCollection<AdminProductModerationResultResponse> ModerationResults,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminProductRevisionDetailResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    AdminProductSellerResponse Seller,
    string Status,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    AdminProductListingSnapshotResponse Current,
    AdminProductListingSnapshotResponse Proposed,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminProductListingSnapshotResponse(
    Guid? CategoryId,
    string? CategoryPath,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    string? SeoTitle,
    string? SeoDescription,
    string? MerchandisingLabel,
    string? CareInstructions,
    string? ProductDisclaimer,
    IReadOnlyCollection<string> Tags,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<AdminProductRevisionImageResponse> Images);

public sealed record AdminProductVariantRevisionDetailResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    AdminProductSellerResponse Seller,
    string? ProductTitle,
    string? ProductSlug,
    string Status,
    string? SellerReason,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    IReadOnlyCollection<AdminProductVariantRevisionFinalVariantResponse> CurrentVariants,
    IReadOnlyCollection<AdminProductVariantRevisionItemResponse> Items,
    IReadOnlyCollection<AdminProductVariantRevisionFinalVariantResponse> ProposedFinalVariants,
    IReadOnlyDictionary<string, string[]> ValidationErrors,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminProductVariantRevisionItemResponse(
    Guid RevisionItemId,
    string Operation,
    Guid? SourceVariantId,
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int? InitialStockQuantity,
    string ProposedStatus,
    string? Barcode);

public sealed record AdminProductVariantRevisionFinalVariantResponse(
    Guid? SourceVariantId,
    string ChangeType,
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    int ReservedQuantity,
    string Status,
    string? Barcode,
    int AvailableQuantity);

public sealed record AdminProductSellerResponse(
    string? DisplayName,
    string? ContactEmail,
    string? VerificationStatus);

public sealed record AdminProductVariantResponse(
    Guid VariantId,
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    int ReservedQuantity,
    string Status,
    int AvailableQuantity);

public sealed record AdminProductImageResponse(
    Guid ImageId,
    string Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminProductRevisionImageResponse(
    Guid ImageId,
    string Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminProductModerationResultResponse(
    Guid ModerationResultId,
    string RiskLevel,
    bool NeedsAdminReview,
    string Reason,
    IReadOnlyCollection<string> DetectedTerms,
    IReadOnlyCollection<string> MissingFields,
    IReadOnlyCollection<string> Flags,
    string Provider,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminProductApproveRequest(string? OverrideReason = null);

public sealed record AdminProductReasonRequest(string Reason);
