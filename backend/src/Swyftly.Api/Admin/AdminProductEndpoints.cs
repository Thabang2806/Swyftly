using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Application.Ai;
using Swyftly.Application.Catalog;
using Swyftly.Application.Identity;
using Swyftly.Application.Search;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Admin;

public static class AdminProductEndpoints
{
    public static IEndpointRouteBuilder MapAdminProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/products")
            .WithTags("Admin Products")
            .RequireAuthorization(SwyftlyPolicies.AdminOnly);

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

    private static async Task<IResult> GetPendingReviewAsync(
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateRevisionDetailResponseAsync(revisionId, dbContext, cancellationToken);
        return detail is null ? ProductNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveRevisionAsync(
        Guid revisionId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        IProductSearchIndexer productSearchIndexer,
        IProductEmbeddingGenerator productEmbeddingGenerator,
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
                revision.TagsJson);
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
                revision.AttributesJson
            }),
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await productSearchIndexer.IndexProductAsync(product.Id, cancellationToken);
        await productEmbeddingGenerator.GenerateForProductAsync(product.Id, cancellationToken);

        return HttpResults.Ok(await CreateRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectRevisionAsync(
        Guid revisionId,
        AdminProductReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
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
        return HttpResults.Ok(await CreateRevisionDetailResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        IProductSearchIndexer productSearchIndexer,
        IProductEmbeddingGenerator productEmbeddingGenerator,
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
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid productId,
        AdminProductReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
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
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RequestChangesAsync(
        Guid productId,
        AdminProductReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
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
        return HttpResults.Ok(await CreateDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<AdminProductDetailResponse?> CreateDetailResponseAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
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
                ReadStringArray(revision.TagsJson),
                ReadRevisionAttributes(revision.AttributesJson),
                proposedImages),
            auditTrail);
    }

    private static async Task<IResult?> ValidateRevisionForApprovalAsync(
        ProductListingRevision revision,
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
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
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.AiModerationResults.AnyAsync(
            result => result.ProductId == productId
                && result.NeedsAdminReview
                && result.RiskLevel == AiModerationRiskLevel.High,
            cancellationToken);

    private static async Task<string?> GetCategoryPathAsync(
        Guid? categoryId,
        SwyftlyDbContext dbContext,
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
        var actorRole = principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : SwyftlyRoles.Admin;

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
        var actorRole = principal.IsInRole(SwyftlyRoles.SuperAdmin)
            ? SwyftlyRoles.SuperAdmin
            : SwyftlyRoles.Admin;

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
    IReadOnlyCollection<string> Tags,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<AdminProductRevisionImageResponse> Images);

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
