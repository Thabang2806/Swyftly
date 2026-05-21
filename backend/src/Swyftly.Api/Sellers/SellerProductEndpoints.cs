using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Swyftly.Api.Results;
using Swyftly.Api.Security;
using Swyftly.Application.Abstractions;
using Swyftly.Application.Ai;
using Swyftly.Application.Catalog;
using Swyftly.Application.Identity;
using Swyftly.Application.Media;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Media;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Sellers;

public static class SellerProductEndpoints
{
    private const int MaxRevisionImages = 12;

    private static readonly HashSet<string> SupportedAiApplyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "shortdescription",
        "fulldescription",
        "category",
        "attributes",
        "tags",
        "imagealttext"
    };

    public static IEndpointRouteBuilder MapSellerProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/products")
            .WithTags("Seller Products")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        group.MapPost("", CreateProductAsync)
            .WithName("CreateSellerProduct")
            .WithSummary("Creates a seller-owned product draft.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.ProductWrite)
            .Produces<SellerProductDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("", ListProductsAsync)
            .WithName("ListSellerProducts")
            .WithSummary("Lists products owned by the current seller.")
            .Produces<IReadOnlyCollection<SellerProductSummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetProductAsync)
            .WithName("GetSellerProduct")
            .WithSummary("Returns a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateProductAsync)
            .WithName("UpdateSellerProduct")
            .WithSummary("Updates editable product draft details and category attribute values.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/variants", AddVariantAsync)
            .WithName("AddSellerProductVariant")
            .WithSummary("Adds a variant to a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/variants/{variantId:guid}", UpdateVariantAsync)
            .WithName("UpdateSellerProductVariant")
            .WithSummary("Updates a product variant on a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/variants/{variantId:guid}", DeleteVariantAsync)
            .WithName("DeleteSellerProductVariant")
            .WithSummary("Removes a product variant from a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/images", AddImageAsync)
            .WithName("AddSellerProductImage")
            .WithSummary("Attaches an image record to a seller-owned product draft after upload.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/images/upload", UploadImageAsync)
            .WithName("UploadSellerProductImage")
            .WithSummary("Uploads a local product image for a seller-owned editable product.")
            .DisableAntiforgery()
            .RequireRateLimiting(SwyftlyRateLimitPolicies.ProductWrite)
            .Produces<SellerProductDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/images/{imageId:guid}", UpdateImageAsync)
            .WithName("UpdateSellerProductImage")
            .WithSummary("Updates image alt text, sort order, and primary status on a seller-owned editable product.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/images/{imageId:guid}", DeleteImageAsync)
            .WithName("DeleteSellerProductImage")
            .WithSummary("Removes a product image from a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/submit-review", SubmitReviewAsync)
            .WithName("SubmitSellerProductForReview")
            .WithSummary("Submits a seller-owned product for marketplace review.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/ai-suggestions", GenerateAiSuggestionAsync)
            .WithName("GenerateSellerProductAiSuggestion")
            .WithSummary("Generates and saves an AI listing suggestion for a seller-owned product draft.")
            .WithDescription("Uses a stricter AI rate-limit policy because AI provider calls are more expensive and abuse-prone than normal browsing.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.Ai)
            .Produces<SellerAiSuggestionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/ai-suggestions/{suggestionId:guid}/apply", ApplyAiSuggestionAsync)
            .WithName("ApplySellerProductAiSuggestion")
            .WithSummary("Applies selected AI suggestion fields to a seller-owned product draft.")
            .Produces<SellerProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/revision", GetRevisionAsync)
            .WithName("GetSellerProductListingRevision")
            .WithSummary("Returns or creates the active seller listing revision for a published product.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/revision", UpdateRevisionAsync)
            .WithName("UpdateSellerProductListingRevision")
            .WithSummary("Stages listing content changes for a published product revision.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/revision/images/upload", UploadRevisionImageAsync)
            .WithName("UploadSellerProductListingRevisionImage")
            .WithSummary("Uploads an image into a published product listing revision.")
            .DisableAntiforgery()
            .RequireRateLimiting(SwyftlyRateLimitPolicies.ProductWrite)
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/revision/images/{revisionImageId:guid}", UpdateRevisionImageAsync)
            .WithName("UpdateSellerProductListingRevisionImage")
            .WithSummary("Updates image metadata in a published product listing revision.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/revision/images/{revisionImageId:guid}", DeleteRevisionImageAsync)
            .WithName("DeleteSellerProductListingRevisionImage")
            .WithSummary("Removes an image from a published product listing revision.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/revision/submit-review", SubmitRevisionReviewAsync)
            .WithName("SubmitSellerProductListingRevision")
            .WithSummary("Submits a published product listing revision for admin review.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/revision/cancel", CancelRevisionAsync)
            .WithName("CancelSellerProductListingRevision")
            .WithSummary("Cancels the active published product listing revision.")
            .Produces<SellerProductRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateProductAsync(
        UpsertSellerProductRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var categoryValidation = await ValidateCategoryAsync(request.CategoryId, dbContext, cancellationToken);
        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        var product = new Product(seller.Id);
        try
        {
            product.UpdateDraftDetails(
                request.CategoryId,
                request.BrandId,
                request.Title,
                request.Slug,
                request.ShortDescription,
                request.FullDescription);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("product", exception.Message);
        }

        dbContext.Products.Add(product);
        UpsertAttributeValues(product.Id, request.Attributes, dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken);
        return HttpResults.Created($"/api/seller/products/{product.Id}", response);
    }

    private static async Task<IResult> ListProductsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var products = await dbContext.Products
            .Where(product => product.SellerId == seller.Id)
            .OrderByDescending(product => product.UpdatedAtUtc)
            .Select(product => new SellerProductSummaryResponse(
                product.Id,
                product.CategoryId,
                product.Title,
                product.Slug,
                product.Status.ToString(),
                product.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(products);
    }

    private static async Task<IResult> GetProductAsync(
        Guid id,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        return product is null
            ? ProductNotFound()
            : HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid id,
        UpsertSellerProductRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var categoryValidation = await ValidateCategoryAsync(request.CategoryId, dbContext, cancellationToken);
        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        try
        {
            product.UpdateDraftDetails(
                request.CategoryId,
                request.BrandId,
                request.Title,
                request.Slug,
                request.ShortDescription,
                request.FullDescription);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("product", exception.Message);
        }

        UpsertAttributeValues(product.Id, request.Attributes, dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeleteVariantAsync(
        Guid id,
        Guid variantId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can remove variants only from draft or rejected products.");
        }

        var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
            item => item.Id == variantId && item.ProductId == product.Id,
            cancellationToken);
        if (variant is null)
        {
            return VariantNotFound();
        }

        dbContext.ProductVariants.Remove(variant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> AddVariantAsync(
        Guid id,
        UpsertSellerProductVariantRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can add variants only to draft or rejected products.");
        }

        if (!TryCreateVariantCommand(request, out var command, out var error))
        {
            return Validation("variant", error);
        }

        var duplicate = await HasDuplicateVariantAsync(product.Id, null, command!, dbContext, cancellationToken);
        if (duplicate is not null)
        {
            return Validation("variant", duplicate);
        }

        ProductVariant variant;
        try
        {
            variant = new ProductVariant(
                product.Id,
                command!.Sku,
                command.Size,
                command.Colour,
                command.Price,
                command.CompareAtPrice,
                command.StockQuantity,
                command.ReservedQuantity,
                command.Status,
                command.Barcode);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("variant", exception.Message);
        }

        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created(
            $"/api/seller/products/{product.Id}/variants/{variant.Id}",
            await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateVariantAsync(
        Guid id,
        Guid variantId,
        UpsertSellerProductVariantRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can update variants only on draft or rejected products.");
        }

        var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
            item => item.Id == variantId && item.ProductId == product.Id,
            cancellationToken);
        if (variant is null)
        {
            return VariantNotFound();
        }

        if (!TryCreateVariantCommand(request, out var command, out var error))
        {
            return Validation("variant", error);
        }

        var duplicate = await HasDuplicateVariantAsync(product.Id, variant.Id, command!, dbContext, cancellationToken);
        if (duplicate is not null)
        {
            return Validation("variant", duplicate);
        }

        try
        {
            variant.Update(
                command!.Sku,
                command.Size,
                command.Colour,
                command.Price,
                command.CompareAtPrice,
                command.StockQuantity,
                command.ReservedQuantity,
                command.Status,
                command.Barcode);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("variant", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> AddImageAsync(
        Guid id,
        AttachSellerProductImageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IImageStorageProvider imageStorageProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can attach images only to draft or rejected products.");
        }

        ImageStorageReference reference;
        try
        {
            var command = new AttachProductImageCommand(
                request.StorageKey,
                request.Url,
                request.AltText,
                request.SortOrder,
                request.IsPrimary);

            reference = await imageStorageProvider.CreateReferenceAsync(
                new CreateImageReferenceRequest(command.StorageKey, command.Url),
                cancellationToken);

            dbContext.ProductImages.Add(new ProductImage(
                product.Id,
                reference.Url,
                reference.StorageKey,
                command.AltText,
                command.SortOrder,
                command.IsPrimary,
                timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("image", exception.Message);
        }

        if (request.IsPrimary)
        {
            await ClearPrimaryProductImagesAsync(product.Id, null, dbContext, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created(
            $"/api/seller/products/{product.Id}/images",
            await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UploadImageAsync(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? altText,
        [FromForm] int sortOrder,
        [FromForm] bool isPrimary,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IProductMediaUploadService mediaUploadService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can upload images only to draft, rejected, or changes-requested products.");
        }

        if (sortOrder < 0)
        {
            return Validation("image", "Sort order cannot be negative.");
        }

        ProductMediaUploadResult uploadResult;
        try
        {
            await using var stream = file.OpenReadStream();
            uploadResult = await mediaUploadService.UploadAsync(
                new ProductMediaUploadRequest(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    $"seller-{product.SellerId}/product-{product.Id}",
                    product.SellerId,
                    product.Id,
                    null),
                cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Validation("image", exception.Message);
        }

        if (isPrimary)
        {
            await ClearPrimaryProductImagesAsync(product.Id, null, dbContext, cancellationToken);
        }

        dbContext.MediaAssets.Add(uploadResult.Asset);
        dbContext.MediaAssetVariants.AddRange(uploadResult.Variants);
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            uploadResult.DetailVariant.PublicUrl,
            uploadResult.DetailVariant.StorageKey,
            altText,
            sortOrder,
            isPrimary,
            timeProvider.GetUtcNow(),
            uploadResult.Asset.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created(
            $"/api/seller/products/{product.Id}/images",
            await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeleteImageAsync(
        Guid id,
        Guid imageId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IImageStorageProvider imageStorageProvider,
        IProductMediaUploadService mediaUploadService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can remove images only from draft or rejected products.");
        }

        var image = await dbContext.ProductImages.SingleOrDefaultAsync(
            item => item.Id == imageId && item.ProductId == product.Id,
            cancellationToken);
        if (image is null)
        {
            return ImageNotFound();
        }

        MediaAsset? mediaAsset = null;
        List<MediaAssetVariant> mediaVariants = [];
        if (image.MediaAssetId.HasValue)
        {
            mediaAsset = await dbContext.MediaAssets.SingleOrDefaultAsync(
                asset => asset.Id == image.MediaAssetId.Value,
                cancellationToken);
            mediaVariants = await dbContext.MediaAssetVariants
                .Where(variant => variant.MediaAssetId == image.MediaAssetId.Value)
                .ToListAsync(cancellationToken);
        }

        dbContext.ProductImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (mediaAsset is not null)
        {
            await mediaUploadService.DeleteAsync(mediaAsset, mediaVariants, timeProvider.GetUtcNow(), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await imageStorageProvider.DeleteAsync(image.StorageKey, cancellationToken);
        }

        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateImageAsync(
        Guid id,
        Guid imageId,
        UpdateSellerProductImageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "Seller can update images only on draft, rejected, or changes-requested products.");
        }

        var image = await dbContext.ProductImages.SingleOrDefaultAsync(
            item => item.Id == imageId && item.ProductId == product.Id,
            cancellationToken);
        if (image is null)
        {
            return ImageNotFound();
        }

        try
        {
            image.UpdateMetadata(request.AltText, request.SortOrder);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Validation("image", exception.Message);
        }

        if (request.IsPrimary)
        {
            await ClearPrimaryProductImagesAsync(product.Id, image.Id, dbContext, cancellationToken);
            image.MarkPrimary();
        }
        else
        {
            image.ClearPrimary();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task ClearPrimaryProductImagesAsync(
        Guid productId,
        Guid? exceptImageId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var images = await dbContext.ProductImages
            .Where(item => item.ProductId == productId && item.Id != exceptImageId && item.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            image.ClearPrimary();
        }
    }

    private static async Task<IResult> SubmitReviewAsync(
        Guid id,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        ProductModerationService productModerationService,
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
            return HttpResults.Problem(
                title: "SellerProducts.SellerNotVerified",
                detail: "Only verified sellers can submit products for review. Pending sellers may create drafts.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.SellerId == seller.Id,
            cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var validationErrors = await ValidateProductForSubmissionAsync(product, dbContext, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var hasImage = await dbContext.ProductImages.AnyAsync(image => image.ProductId == product.Id, cancellationToken);
        var hasActiveVariantWithStock = await dbContext.ProductVariants.AnyAsync(
            variant => variant.ProductId == product.Id
                && variant.Status == ProductVariantStatus.Active
                && variant.StockQuantity > variant.ReservedQuantity,
            cancellationToken);

        try
        {
            var moderationRequest = await CreateProductModerationRequestAsync(
                product,
                dbContext,
                cancellationToken);
            var moderationDecision = productModerationService.Moderate(moderationRequest);
            dbContext.AiModerationResults.Add(new AiModerationResult(
                product.Id,
                product.SellerId,
                moderationDecision.RiskLevel,
                moderationDecision.NeedsAdminReview,
                moderationDecision.Reason,
                JsonSerializer.Serialize(moderationDecision.DetectedTerms),
                JsonSerializer.Serialize(moderationDecision.MissingFields),
                JsonSerializer.Serialize(moderationDecision.Flags.Select(flag => new
                {
                    flag.Code,
                    RiskLevel = flag.RiskLevel.ToString(),
                    flag.Message,
                    flag.Terms
                })),
                moderationDecision.Provider,
                timeProvider.GetUtcNow()));

            product.SubmitForReview(
                hasImage,
                hasActiveVariantWithStock,
                moderationDecision.NeedsAdminReview);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("product", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetRevisionAsync(
        Guid id,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Published listing revisions are available only for published products.");
        }

        var revision = await GetOrCreateActiveRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateRevisionAsync(
        Guid id,
        UpsertSellerProductRevisionRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Listing revisions can be staged only for published products.");
        }

        var categoryValidation = await ValidateCategoryAsync(request.CategoryId, dbContext, cancellationToken);
        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        var revision = await GetOrCreateActiveRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        if (!revision.CanSellerEdit)
        {
            return Validation("revision", "A revision already submitted for review cannot be edited.");
        }

        var tagsJson = request.Tags is null
            ? revision.TagsJson
            : JsonSerializer.Serialize(NormalizeStringArray(request.Tags));
        var attributes = request.Attributes ?? ParseAttributesJson(revision.AttributesJson);
        var attributeValidation = request.CategoryId.HasValue
            ? await ValidateAttributesAsync(request.CategoryId.Value, attributes, dbContext, cancellationToken)
            : null;
        if (attributeValidation is not null)
        {
            return attributeValidation;
        }

        try
        {
            revision.UpdateProposal(
                request.CategoryId,
                request.BrandId,
                request.Title,
                request.Slug,
                request.ShortDescription,
                request.FullDescription,
                tagsJson,
                SerializeAttributes(attributes));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            return Validation("revision", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UploadRevisionImageAsync(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? altText,
        [FromForm] int sortOrder,
        [FromForm] bool isPrimary,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IImageStorageProvider imageStorageProvider,
        IProductMediaUploadService mediaUploadService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Revision image uploads are available only for published products.");
        }

        if (sortOrder < 0)
        {
            return Validation("image", "Sort order cannot be negative.");
        }

        var revision = await GetOrCreateActiveRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        if (!revision.CanSellerEdit)
        {
            return Validation("revision", "A revision already submitted for review cannot be edited.");
        }

        var imageCount = await dbContext.ProductListingRevisionImages.CountAsync(
            image => image.RevisionId == revision.Id,
            cancellationToken);
        if (imageCount >= MaxRevisionImages)
        {
            return Validation("image", $"A revision can contain at most {MaxRevisionImages} images.");
        }

        ProductMediaUploadResult uploadResult;
        try
        {
            await using var stream = file.OpenReadStream();
            uploadResult = await mediaUploadService.UploadAsync(
                new ProductMediaUploadRequest(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    $"seller-{product.SellerId}/product-{product.Id}/revision-{revision.Id}",
                    product.SellerId,
                    product.Id,
                    revision.Id),
                cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Validation("image", exception.Message);
        }

        if (isPrimary)
        {
            await ClearPrimaryRevisionImagesAsync(revision.Id, null, dbContext, cancellationToken);
        }

        dbContext.MediaAssets.Add(uploadResult.Asset);
        dbContext.MediaAssetVariants.AddRange(uploadResult.Variants);
        dbContext.ProductListingRevisionImages.Add(new ProductListingRevisionImage(
            revision.Id,
            null,
            uploadResult.DetailVariant.PublicUrl,
            uploadResult.DetailVariant.StorageKey,
            altText,
            sortOrder,
            isPrimary,
            timeProvider.GetUtcNow(),
            uploadResult.Asset.Id));

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Created(
            $"/api/seller/products/{product.Id}/revision/images",
            await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateRevisionImageAsync(
        Guid id,
        Guid revisionImageId,
        UpdateSellerProductImageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revisionResult = await GetEditableRevisionForProductAsync(id, principal, dbContext, timeProvider, cancellationToken);
        if (revisionResult.Result is not null)
        {
            return revisionResult.Result;
        }

        var revision = revisionResult.Revision!;
        var image = await dbContext.ProductListingRevisionImages.SingleOrDefaultAsync(
            item => item.Id == revisionImageId && item.RevisionId == revision.Id,
            cancellationToken);
        if (image is null)
        {
            return ImageNotFound();
        }

        try
        {
            image.UpdateMetadata(request.AltText, request.SortOrder);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Validation("image", exception.Message);
        }

        if (request.IsPrimary)
        {
            await ClearPrimaryRevisionImagesAsync(revision.Id, image.Id, dbContext, cancellationToken);
            image.MarkPrimary();
        }
        else
        {
            image.ClearPrimary();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeleteRevisionImageAsync(
        Guid id,
        Guid revisionImageId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IImageStorageProvider imageStorageProvider,
        IProductMediaUploadService mediaUploadService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revisionResult = await GetEditableRevisionForProductAsync(id, principal, dbContext, timeProvider, cancellationToken);
        if (revisionResult.Result is not null)
        {
            return revisionResult.Result;
        }

        var revision = revisionResult.Revision!;
        var image = await dbContext.ProductListingRevisionImages.SingleOrDefaultAsync(
            item => item.Id == revisionImageId && item.RevisionId == revision.Id,
            cancellationToken);
        if (image is null)
        {
            return ImageNotFound();
        }

        MediaAsset? mediaAsset = null;
        List<MediaAssetVariant> mediaVariants = [];
        if (image.MediaAssetId.HasValue)
        {
            mediaAsset = await dbContext.MediaAssets.SingleOrDefaultAsync(
                asset => asset.Id == image.MediaAssetId.Value,
                cancellationToken);
            mediaVariants = await dbContext.MediaAssetVariants
                .Where(variant => variant.MediaAssetId == image.MediaAssetId.Value)
                .ToListAsync(cancellationToken);
        }

        dbContext.ProductListingRevisionImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (mediaAsset is not null)
        {
            await mediaUploadService.DeleteAsync(mediaAsset, mediaVariants, timeProvider.GetUtcNow(), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!image.SourceProductImageId.HasValue)
        {
            await imageStorageProvider.DeleteAsync(image.StorageKey, cancellationToken);
        }

        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> SubmitRevisionReviewAsync(
        Guid id,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Only published products can submit listing revisions.");
        }

        var revision = await GetActiveRevisionAsync(product.Id, dbContext, cancellationToken);
        if (revision is null)
        {
            return Validation("revision", "Create a listing revision before submitting it for review.");
        }

        if (revision.CategoryId.HasValue)
        {
            var categoryValidation = await ValidateCategoryAsync(revision.CategoryId, dbContext, cancellationToken);
            if (categoryValidation is not null)
            {
                return categoryValidation;
            }

            var attributes = ParseAttributesJson(revision.AttributesJson);
            var attributeValidation = await ValidateAttributesAsync(revision.CategoryId.Value, attributes, dbContext, cancellationToken);
            if (attributeValidation is not null)
            {
                return attributeValidation;
            }
        }

        var hasImage = await dbContext.ProductListingRevisionImages.AnyAsync(
            image => image.RevisionId == revision.Id,
            cancellationToken);

        try
        {
            revision.SubmitForReview(hasImage, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Validation("revision", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> CancelRevisionAsync(
        Guid id,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Only published products can cancel listing revisions.");
        }

        var revision = await GetOrCreateActiveRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        revision.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GenerateAiSuggestionAsync(
        Guid id,
        GenerateSellerAiSuggestionRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IAiListingAssistantService aiListingAssistantService,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.SellerId == seller.Id,
            cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "AI suggestions can be generated only for draft or rejected products.");
        }

        var categoryHintId = request.SelectedCategoryId ?? product.CategoryId;
        var categoryValidation = await ValidateCategoryAsync(categoryHintId, dbContext, cancellationToken);
        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        var images = await GetAiSuggestionImageReferencesAsync(
            product.Id,
            request.ImageIds,
            dbContext,
            cancellationToken);
        if (images.ValidationResult is not null)
        {
            return images.ValidationResult;
        }

        var knownAttributes = await BuildAiKnownAttributesAsync(
            product,
            request,
            dbContext,
            cancellationToken);
        var categories = await BuildAiCategoryReferencesAsync(dbContext, cancellationToken);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await aiListingAssistantService.GenerateSuggestionAsync(
            new AiListingAssistantRequest(
                seller.Id,
                product.Id,
                request.SellerNotes,
                request.ProductTypeHint,
                knownAttributes,
                categoryHintId,
                images.ImageReferences,
                categories,
                UserId: userId),
            cancellationToken);

        return result.ToHttpResult(suggestion => HttpResults.Ok(MapAiSuggestionResponse(suggestion)));
    }

    private static async Task<IResult> ApplyAiSuggestionAsync(
        Guid id,
        Guid suggestionId,
        ApplySellerAiSuggestionRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var fieldsToApply = NormalizeFields(request.FieldsToApply);
        if (fieldsToApply.Count == 0)
        {
            return Validation("fieldsToApply", "At least one field must be selected.");
        }

        var unknownFields = fieldsToApply.Except(SupportedAiApplyFields).ToArray();
        if (unknownFields.Length > 0)
        {
            return Validation("fieldsToApply", $"Unsupported AI suggestion field: {string.Join(", ", unknownFields)}.");
        }

        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.SellerId == seller.Id,
            cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (!product.CanSellerEdit)
        {
            return Validation("product", "AI suggestions can be applied only to draft or rejected products.");
        }

        var suggestion = await dbContext.AiProductSuggestions.SingleOrDefaultAsync(
            item => item.Id == suggestionId && item.ProductId == product.Id && item.SellerId == seller.Id,
            cancellationToken);
        if (suggestion is null)
        {
            return HttpResults.Problem(
                title: "SellerProducts.AiSuggestionNotFound",
                detail: "AI suggestion was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (suggestion.Status is AiProductSuggestionStatus.Applied or AiProductSuggestionStatus.Rejected)
        {
            return Validation("suggestion", "Only draft or accepted AI suggestions can be applied.");
        }

        var riskFlags = ReadStringArray(suggestion.RiskFlagsJson);
        if (riskFlags.Count > 0 && !request.ConfirmRiskFlags)
        {
            return Validation("riskFlags", "This AI suggestion has risk flags. Confirm risk flags before applying selected fields.");
        }

        var audits = new List<AiSuggestionFieldAudit>();
        var now = timeProvider.GetUtcNow();
        var title = product.Title;
        var shortDescription = product.ShortDescription;
        var fullDescription = product.FullDescription;
        var categoryId = product.CategoryId;

        if (fieldsToApply.Contains("title"))
        {
            var finalValue = ReadEditedString(request, "title", "recommendedTitle") ?? suggestion.SuggestedTitle;
            title = finalValue;
            audits.Add(CreateAudit(suggestion.Id, "title", suggestion.SuggestedTitle, finalValue, now));
        }

        if (fieldsToApply.Contains("shortdescription"))
        {
            var finalValue = ReadEditedString(request, "shortDescription") ?? suggestion.SuggestedShortDescription;
            shortDescription = finalValue;
            audits.Add(CreateAudit(suggestion.Id, "shortDescription", suggestion.SuggestedShortDescription, finalValue, now));
        }

        if (fieldsToApply.Contains("fulldescription"))
        {
            var finalValue = ReadEditedString(request, "fullDescription") ?? suggestion.SuggestedFullDescription;
            fullDescription = finalValue;
            audits.Add(CreateAudit(suggestion.Id, "fullDescription", suggestion.SuggestedFullDescription, finalValue, now));
        }

        if (fieldsToApply.Contains("category"))
        {
            var finalValue = ReadEditedGuid(request, "categoryId", "suggestedCategoryId") ?? suggestion.SuggestedCategoryId;
            if (!finalValue.HasValue)
            {
                return Validation("category", "AI suggestion does not contain a category to apply.");
            }

            var categoryValidation = await ValidateCategoryAsync(finalValue, dbContext, cancellationToken);
            if (categoryValidation is not null)
            {
                return categoryValidation;
            }

            categoryId = finalValue;
            audits.Add(CreateAudit(suggestion.Id, "category", suggestion.SuggestedCategoryId, finalValue, now));
        }

        IReadOnlyDictionary<string, JsonElement>? finalAttributes = null;
        if (fieldsToApply.Contains("attributes"))
        {
            if (!categoryId.HasValue)
            {
                return Validation("attributes", "A category is required before attributes can be applied.");
            }

            finalAttributes = await BuildFinalAttributesAsync(product.Id, suggestion, request, dbContext, cancellationToken);
            var attributeValidation = await ValidateAttributesAsync(categoryId.Value, finalAttributes, dbContext, cancellationToken);
            if (attributeValidation is not null)
            {
                return attributeValidation;
            }

            audits.Add(CreateAudit(
                suggestion.Id,
                "attributes",
                suggestion.SuggestedAttributesJson,
                JsonSerializer.Serialize(finalAttributes.ToDictionary(item => item.Key, item => ToValidationValue(item.Value))),
                now));
        }

        try
        {
            product.UpdateDraftDetails(
                categoryId,
                product.BrandId,
                title,
                product.Slug,
                shortDescription,
                fullDescription);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Validation("product", exception.Message);
        }

        if (finalAttributes is not null)
        {
            UpsertAttributeValues(product.Id, finalAttributes, dbContext);
        }

        if (fieldsToApply.Contains("tags"))
        {
            var tags = ReadEditedStringArray(request, "tags") ?? ReadStringArray(suggestion.SuggestedTagsJson);
            product.UpdateTags(JsonSerializer.Serialize(tags));
            audits.Add(CreateAudit(suggestion.Id, "tags", suggestion.SuggestedTagsJson, JsonSerializer.Serialize(tags), now));
        }

        if (fieldsToApply.Contains("imagealttext"))
        {
            var imageAltText = ReadEditedImageAltText(request);
            if (imageAltText.Count == 0)
            {
                return Validation("imageAltText", "Edited image alt text is required because AI image alt text is not stored yet.");
            }

            var images = await dbContext.ProductImages
                .Where(image => image.ProductId == product.Id && imageAltText.Keys.Contains(image.Id))
                .ToListAsync(cancellationToken);
            if (images.Count != imageAltText.Count)
            {
                return Validation("imageAltText", "All image alt text entries must belong to the selected product.");
            }

            foreach (var image in images)
            {
                image.UpdateAltText(imageAltText[image.Id]);
            }

            audits.Add(CreateAudit(
                suggestion.Id,
                "imageAltText",
                null,
                JsonSerializer.Serialize(imageAltText),
                now));
        }

        if (suggestion.Status == AiProductSuggestionStatus.Draft)
        {
            suggestion.Accept(now);
        }

        suggestion.MarkApplied(now);
        dbContext.AiSuggestionFieldAudits.AddRange(audits);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateProductDetailResponseAsync(product.Id, dbContext, cancellationToken));
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await dbContext.SellerProfiles
            .SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken);
    }

    private static async Task<Product?> GetOwnedProductAsync(
        Guid productId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return null;
        }

        return await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == productId && product.SellerId == seller.Id, cancellationToken);
    }

    private static async Task<(ProductListingRevision? Revision, IResult? Result)> GetEditableRevisionForProductAsync(
        Guid productId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(productId, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return (null, ProductNotFound());
        }

        if (product.Status != ProductStatus.Published)
        {
            return (null, Validation("product", "Listing revisions are available only for published products."));
        }

        var revision = await GetOrCreateActiveRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        return revision.CanSellerEdit
            ? (revision, null)
            : (null, Validation("revision", "A revision already submitted for review cannot be edited."));
    }

    private static async Task<ProductListingRevision?> GetActiveRevisionAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.ProductListingRevisions
            .Where(revision => revision.ProductId == productId
                && (revision.Status == ProductListingRevisionStatus.Draft
                    || revision.Status == ProductListingRevisionStatus.PendingReview
                    || revision.Status == ProductListingRevisionStatus.Rejected))
            .OrderByDescending(revision => revision.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<ProductListingRevision> GetOrCreateActiveRevisionAsync(
        Product product,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revision = await GetActiveRevisionAsync(product.Id, dbContext, cancellationToken);
        if (revision is not null)
        {
            return revision;
        }

        revision = new ProductListingRevision(product.Id, product.SellerId);
        revision.UpdateProposal(
            product.CategoryId,
            product.BrandId,
            product.Title,
            product.Slug,
            product.ShortDescription,
            product.FullDescription,
            product.TagsJson,
            await BuildProductAttributesJsonAsync(product.Id, dbContext, cancellationToken));
        dbContext.ProductListingRevisions.Add(revision);

        var productImages = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ToListAsync(cancellationToken);
        foreach (var image in productImages)
        {
            dbContext.ProductListingRevisionImages.Add(new ProductListingRevisionImage(
                revision.Id,
                image.Id,
                image.Url,
                image.StorageKey,
                image.AltText,
                image.SortOrder,
                image.IsPrimary,
                timeProvider.GetUtcNow(),
                image.MediaAssetId));
        }

        return revision;
    }

    private static async Task ClearPrimaryRevisionImagesAsync(
        Guid revisionId,
        Guid? exceptImageId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var images = await dbContext.ProductListingRevisionImages
            .Where(item => item.RevisionId == revisionId && item.Id != exceptImageId && item.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            image.ClearPrimary();
        }
    }

    private static async Task<string> BuildProductAttributesJsonAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var attributeRows = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .OrderBy(attribute => attribute.Key)
            .Select(attribute => new { attribute.Key, attribute.ValueJson })
            .ToListAsync(cancellationToken);
        var attributes = attributeRows.ToDictionary(
            attribute => attribute.Key,
            attribute => JsonDocument.Parse(attribute.ValueJson).RootElement.Clone(),
            StringComparer.OrdinalIgnoreCase);

        return SerializeAttributes(attributes);
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

    private static string SerializeAttributes(IReadOnlyDictionary<string, JsonElement> attributes) =>
        JsonSerializer.Serialize(attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key))
            .ToDictionary(
                attribute => attribute.Key.Trim().ToLowerInvariant(),
                attribute => attribute.Value,
                StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyCollection<string> NormalizeStringArray(IReadOnlyCollection<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private static async Task<SellerProductRevisionResponse> CreateRevisionResponseAsync(
        Guid revisionId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductListingRevisions.SingleAsync(
            item => item.Id == revisionId,
            cancellationToken);
        var images = await dbContext.ProductListingRevisionImages
            .Where(image => image.RevisionId == revision.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new SellerProductRevisionImageResponse(
                image.Id,
                image.SourceProductImageId,
                image.Url,
                image.StorageKey,
                image.AltText,
                image.SortOrder,
                image.IsPrimary,
                image.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new SellerProductRevisionResponse(
            revision.Id,
            revision.ProductId,
            revision.SellerId,
            revision.Status.ToString(),
            revision.CanSellerEdit,
            revision.RejectionReason,
            revision.SubmittedAtUtc,
            revision.ReviewedAtUtc,
            revision.CategoryId,
            revision.BrandId,
            revision.Title,
            revision.Slug,
            revision.ShortDescription,
            revision.FullDescription,
            ReadStringArray(revision.TagsJson),
            ReadRevisionAttributes(revision.AttributesJson),
            images);
    }

    private static async Task<IResult?> ValidateCategoryAsync(
        Guid? categoryId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return null;
        }

        if (categoryId == Guid.Empty)
        {
            return Validation("categoryId", "Category id cannot be empty.");
        }

        var categoryExists = await dbContext.Categories.AnyAsync(
            category => category.Id == categoryId && category.IsActive,
            cancellationToken);

        return categoryExists
            ? null
            : Validation("categoryId", "Category does not exist or is inactive.");
    }

    private static async Task<(IReadOnlyCollection<AiListingImageReference> ImageReferences, IResult? ValidationResult)> GetAiSuggestionImageReferencesAsync(
        Guid productId,
        IReadOnlyCollection<Guid>? imageIds,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProductImages.Where(image => image.ProductId == productId);

        if (imageIds is { Count: > 0 })
        {
            if (imageIds.Any(id => id == Guid.Empty))
            {
                return ([], Validation("imageIds", "Image ids cannot contain empty values."));
            }

            query = query.Where(image => imageIds.Contains(image.Id));
        }

        var images = await query
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new AiListingImageReference(image.Id, image.Url, image.AltText))
            .ToListAsync(cancellationToken);

        if (imageIds is { Count: > 0 })
        {
            var foundIds = images.Select(image => image.ImageId).ToHashSet();
            var missingIds = imageIds.Where(id => !foundIds.Contains(id)).ToArray();
            if (missingIds.Length > 0)
            {
                return ([], Validation("imageIds", "All image ids must belong to the selected product."));
            }
        }

        return (images, null);
    }

    private static async Task<IReadOnlyDictionary<string, object?>> BuildAiKnownAttributesAsync(
        Product product,
        GenerateSellerAiSuggestionRequest request,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingAttributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .OrderBy(attribute => attribute.Key)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => ToValidationValue(attribute.ValueJson),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var requestAttributes = (request.KnownAttributes ?? new Dictionary<string, JsonElement>())
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key))
            .ToDictionary(
                attribute => attribute.Key.Trim(),
                attribute => ToValidationValue(attribute.Value),
                StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object?>
        {
            ["productTitle"] = product.Title,
            ["productShortDescription"] = product.ShortDescription,
            ["productFullDescription"] = product.FullDescription,
            ["productCategoryId"] = product.CategoryId,
            ["productBrandId"] = product.BrandId,
            ["productTypeHint"] = request.ProductTypeHint,
            ["existingAttributes"] = existingAttributes,
            ["sellerKnownAttributes"] = requestAttributes
        };
    }

    private static async Task<IReadOnlyCollection<AiListingCategoryReference>> BuildAiCategoryReferencesAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.DisplayOrder)
            .ToListAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id);
        var attributes = await dbContext.CategoryAttributes
            .Where(attribute => attribute.IsActive)
            .OrderBy(attribute => attribute.DisplayOrder)
            .ToListAsync(cancellationToken);
        var attributesByCategoryId = attributes
            .GroupBy(attribute => attribute.CategoryId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return categories
            .Select(category => new AiListingCategoryReference(
                category.Id,
                category.Name,
                category.Slug,
                BuildCategoryPath(category, categoriesById),
                attributesByCategoryId.TryGetValue(category.Id, out var categoryAttributes)
                    ? categoryAttributes.Select(attribute => new AiListingCategoryAttributeReference(
                        attribute.Key,
                        attribute.Name,
                        attribute.DataType.ToString(),
                        attribute.IsRequired,
                        attribute.AllowedValues)).ToArray()
                    : []))
            .ToArray();
    }

    private static async Task<ProductModerationRequest> CreateProductModerationRequestAsync(
        Product product,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categoryPath = await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken);
        var attributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .OrderBy(attribute => attribute.Key)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => ToValidationValue(attribute.ValueJson),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var imageAltTexts = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id && image.AltText != null)
            .Select(image => image.AltText!)
            .ToListAsync(cancellationToken);

        return new ProductModerationRequest(
            product.Id,
            product.SellerId,
            categoryPath,
            product.Title,
            product.ShortDescription,
            product.FullDescription,
            attributes,
            ReadStringArray(product.TagsJson),
            imageAltTexts);
    }

    private static async Task<string?> GetCategoryPathAsync(
        Guid? categoryId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var categories = await dbContext.Categories
            .ToListAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id);

        return categoriesById.TryGetValue(categoryId.Value, out var category)
            ? BuildCategoryPath(category, categoriesById)
            : null;
    }

    private static string BuildCategoryPath(
        Category category,
        IReadOnlyDictionary<Guid, Category> categoriesById)
    {
        var names = new Stack<string>();
        var current = category;

        while (true)
        {
            names.Push(current.Name);
            if (!current.ParentCategoryId.HasValue ||
                !categoriesById.TryGetValue(current.ParentCategoryId.Value, out current!))
            {
                break;
            }
        }

        return string.Join(" > ", names);
    }

    private static void UpsertAttributeValues(
        Guid productId,
        IReadOnlyDictionary<string, JsonElement>? attributes,
        SwyftlyDbContext dbContext)
    {
        if (attributes is null)
        {
            return;
        }

        var existingAttributes = dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .ToDictionary(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in attributes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var normalizedKey = key.Trim().ToLowerInvariant();
            var valueJson = value.GetRawText();

            if (existingAttributes.TryGetValue(normalizedKey, out var existing))
            {
                existing.UpdateValue(valueJson);
            }
            else
            {
                dbContext.ProductAttributeValues.Add(new ProductAttributeValue(productId, normalizedKey, valueJson));
            }
        }

        var requestKeys = attributes.Keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleAttribute in existingAttributes.Values.Where(attribute => !requestKeys.Contains(attribute.Key)))
        {
            dbContext.ProductAttributeValues.Remove(staleAttribute);
        }
    }

    private static async Task<Dictionary<string, string[]>> ValidateProductForSubmissionAsync(
        Product product,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!product.CategoryId.HasValue)
        {
            errors["categoryId"] = ["Category is required."];
            return errors;
        }

        var hasImage = await dbContext.ProductImages.AnyAsync(image => image.ProductId == product.Id, cancellationToken);
        if (!hasImage)
        {
            errors["images"] = ["At least one product image is required."];
        }

        var hasActiveVariantWithStock = await dbContext.ProductVariants.AnyAsync(
            variant => variant.ProductId == product.Id
                && variant.Status == ProductVariantStatus.Active
                && variant.StockQuantity > variant.ReservedQuantity,
            cancellationToken);
        if (!hasActiveVariantWithStock)
        {
            errors["variants"] = ["At least one active variant with stock is required."];
        }

        var definitions = await dbContext.CategoryAttributes
            .Where(attribute => attribute.CategoryId == product.CategoryId && attribute.IsActive)
            .ToListAsync(cancellationToken);
        var values = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => ToValidationValue(attribute.ValueJson),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var attributeValidation = CategoryAttributeValidator.Validate(product.CategoryId.Value, definitions, values);
        if (!attributeValidation.IsValid)
        {
            errors["attributes"] = attributeValidation.Errors.ToArray();
        }

        return errors;
    }

    private static object? ToValidationValue(string valueJson)
    {
        using var document = JsonDocument.Parse(valueJson);
        return ToValidationValue(document.RootElement);
    }

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

    private static SellerAiSuggestionResponse MapAiSuggestionResponse(AiListingSuggestionResponse suggestion) =>
        new(
            suggestion.SuggestionId,
            suggestion.SuggestedTitle,
            string.IsNullOrWhiteSpace(suggestion.SuggestedTitle) ? [] : [suggestion.SuggestedTitle],
            suggestion.SuggestedShortDescription,
            suggestion.SuggestedFullDescription,
            suggestion.SuggestedCategoryId,
            suggestion.SuggestedCategoryPath,
            suggestion.SuggestedAttributes,
            suggestion.SuggestedTags,
            new Dictionary<string, object?>(),
            new Dictionary<Guid, string?>(),
            suggestion.MissingFields,
            suggestion.RiskFlags,
            suggestion.QualityScore);

    private static HashSet<string> NormalizeFields(IReadOnlyCollection<string>? fields) =>
        (fields ?? [])
            .Select(field => field?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant())
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

    private static string? ReadEditedString(
        ApplySellerAiSuggestionRequest request,
        params string[] propertyNames)
    {
        return TryReadEditedValue(request, propertyNames, out var element)
            ? ReadOptionalString(element)
            : null;
    }

    private static Guid? ReadEditedGuid(
        ApplySellerAiSuggestionRequest request,
        params string[] propertyNames)
    {
        if (!TryReadEditedValue(request, propertyNames, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var id) && id != Guid.Empty
            ? id
            : null;
    }

    private static IReadOnlyCollection<string>? ReadEditedStringArray(
        ApplySellerAiSuggestionRequest request,
        params string[] propertyNames)
    {
        return TryReadEditedValue(request, propertyNames, out var element)
            ? ReadStringArray(element)
            : null;
    }

    private static Dictionary<Guid, string?> ReadEditedImageAltText(ApplySellerAiSuggestionRequest request)
    {
        if (!TryReadEditedValue(request, ["imageAltText", "altText"], out var element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var values = new Dictionary<Guid, string?>();
        foreach (var property in element.EnumerateObject())
        {
            if (Guid.TryParse(property.Name, out var imageId) && imageId != Guid.Empty)
            {
                values[imageId] = ReadOptionalString(property.Value);
            }
        }

        return values;
    }

    private static bool TryReadEditedValue(
        ApplySellerAiSuggestionRequest request,
        IReadOnlyCollection<string> propertyNames,
        out JsonElement element)
    {
        element = default;
        if (request.EditedValues is null)
        {
            return false;
        }

        foreach (var propertyName in propertyNames)
        {
            if (request.EditedValues.TryGetValue(propertyName, out element))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadOptionalString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.GetRawText();
    }

    private static IReadOnlyCollection<string> ReadStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadStringArray(document.RootElement);
    }

    private static IReadOnlyCollection<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> BuildFinalAttributesAsync(
        Guid productId,
        AiProductSuggestion suggestion,
        ApplySellerAiSuggestionRequest request,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var finalAttributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => JsonDocument.Parse(attribute.ValueJson).RootElement.Clone(),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        IReadOnlyDictionary<string, JsonElement> appliedAttributes;
        if (TryReadEditedValue(request, ["attributes"], out var editedAttributes) &&
            editedAttributes.ValueKind == JsonValueKind.Object)
        {
            appliedAttributes = editedAttributes
                .EnumerateObject()
                .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                .ToDictionary(
                    property => property.Name.Trim().ToLowerInvariant(),
                    property => property.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            using var document = JsonDocument.Parse(suggestion.SuggestedAttributesJson);
            appliedAttributes = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement
                    .EnumerateObject()
                    .Where(property => !string.IsNullOrWhiteSpace(property.Name))
                    .ToDictionary(
                        property => property.Name.Trim().ToLowerInvariant(),
                        property => property.Value.Clone(),
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, JsonElement>();
        }

        foreach (var (key, value) in appliedAttributes)
        {
            finalAttributes[key] = value;
        }

        return finalAttributes;
    }

    private static async Task<IResult?> ValidateAttributesAsync(
        Guid categoryId,
        IReadOnlyDictionary<string, JsonElement> attributes,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.CategoryAttributes
            .Where(attribute => attribute.CategoryId == categoryId && attribute.IsActive)
            .ToListAsync(cancellationToken);
        var values = attributes.ToDictionary(
            attribute => attribute.Key,
            attribute => ToValidationValue(attribute.Value),
            StringComparer.OrdinalIgnoreCase);

        var validation = CategoryAttributeValidator.Validate(categoryId, definitions, values);
        return validation.IsValid
            ? null
            : HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["attributes"] = validation.Errors.ToArray()
            });
    }

    private static AiSuggestionFieldAudit CreateAudit(
        Guid suggestionId,
        string fieldName,
        object? aiValue,
        object? finalValue,
        DateTimeOffset createdAtUtc)
    {
        var aiJson = SerializeAuditValue(aiValue);
        var finalJson = SerializeAuditValue(finalValue);
        var wasAccepted = string.Equals(aiJson, finalJson, StringComparison.Ordinal);

        return new AiSuggestionFieldAudit(
            suggestionId,
            fieldName,
            aiJson,
            finalJson,
            wasAccepted,
            wasEdited: !wasAccepted,
            createdAtUtc);
    }

    private static string? SerializeAuditValue(object? value)
    {
        return value switch
        {
            null => null,
            string text when LooksLikeJson(text) => text,
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[') || trimmed.StartsWith('"');
    }

    private static bool TryCreateVariantCommand(
        UpsertSellerProductVariantRequest request,
        out UpsertProductVariantCommand? command,
        out string error)
    {
        command = null;
        error = string.Empty;

        if (!Enum.TryParse<ProductVariantStatus>(request.Status, ignoreCase: true, out var status))
        {
            error = "Variant status must be Active, Inactive, or OutOfStock.";
            return false;
        }

        command = new UpsertProductVariantCommand(
            request.Sku,
            request.Size,
            request.Colour,
            request.Price,
            request.CompareAtPrice,
            request.StockQuantity,
            request.ReservedQuantity,
            status,
            request.Barcode);
        return true;
    }

    private static async Task<string?> HasDuplicateVariantAsync(
        Guid productId,
        Guid? currentVariantId,
        UpsertProductVariantCommand command,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var skuExists = await dbContext.ProductVariants.AnyAsync(
            variant => variant.ProductId == productId
                && variant.Id != currentVariantId
                && variant.Sku == command.Sku,
            cancellationToken);
        if (skuExists)
        {
            return "Variant SKU already exists for this product.";
        }

        var sizeColourExists = await dbContext.ProductVariants.AnyAsync(
            variant => variant.ProductId == productId
                && variant.Id != currentVariantId
                && variant.Size == command.Size
                && variant.Colour == command.Colour,
            cancellationToken);

        return sizeColourExists
            ? "A variant with the same size and colour already exists for this product."
            : null;
    }

    private static async Task<SellerProductDetailResponse> CreateProductDetailResponseAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleAsync(product => product.Id == productId, cancellationToken);
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new SellerProductVariantResponse(
                variant.Id,
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

        var images = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new SellerProductImageResponse(
                image.Id,
                image.Url,
                image.StorageKey,
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

        return new SellerProductDetailResponse(
            product.Id,
            product.SellerId,
            product.CategoryId,
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
            images);
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerProducts.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ProductNotFound() =>
        HttpResults.Problem(
            title: "SellerProducts.ProductNotFound",
            detail: "Product was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult VariantNotFound() =>
        HttpResults.Problem(
            title: "SellerProducts.VariantNotFound",
            detail: "Product variant was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ImageNotFound() =>
        HttpResults.Problem(
            title: "SellerProducts.ImageNotFound",
            detail: "Product image was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record UpsertSellerProductRequest(
    Guid? CategoryId,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    IReadOnlyDictionary<string, JsonElement>? Attributes);

public sealed record UpsertSellerProductVariantRequest(
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    int ReservedQuantity,
    string Status,
    string? Barcode);

public sealed record AttachSellerProductImageRequest(
    string StorageKey,
    string? Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary);

public sealed record UpdateSellerProductImageRequest(
    string? AltText,
    int SortOrder,
    bool IsPrimary);

public sealed record UpsertSellerProductRevisionRequest(
    Guid? CategoryId,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    IReadOnlyCollection<string>? Tags,
    IReadOnlyDictionary<string, JsonElement>? Attributes);

public sealed record GenerateSellerAiSuggestionRequest(
    string? SellerNotes,
    string? ProductTypeHint,
    Guid? SelectedCategoryId,
    IReadOnlyDictionary<string, JsonElement>? KnownAttributes,
    IReadOnlyCollection<Guid>? ImageIds);

public sealed record ApplySellerAiSuggestionRequest(
    IReadOnlyCollection<string>? FieldsToApply,
    IReadOnlyDictionary<string, JsonElement>? EditedValues,
    bool ConfirmRiskFlags = false);

public sealed record SellerProductSummaryResponse(
    Guid ProductId,
    Guid? CategoryId,
    string? Title,
    string? Slug,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record SellerProductDetailResponse(
    Guid ProductId,
    Guid SellerId,
    Guid? CategoryId,
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
    IReadOnlyCollection<SellerProductVariantResponse> Variants,
    IReadOnlyCollection<SellerProductImageResponse> Images);

public sealed record SellerProductVariantResponse(
    Guid VariantId,
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

public sealed record SellerProductImageResponse(
    Guid ImageId,
    string Url,
    string StorageKey,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record SellerProductRevisionResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    string Status,
    bool CanEdit,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    Guid? CategoryId,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? FullDescription,
    IReadOnlyCollection<string> Tags,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<SellerProductRevisionImageResponse> Images);

public sealed record SellerProductRevisionImageResponse(
    Guid RevisionImageId,
    Guid? SourceProductImageId,
    string Url,
    string StorageKey,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record SellerAiSuggestionResponse(
    Guid SuggestionId,
    string? RecommendedTitle,
    IReadOnlyCollection<string> TitleSuggestions,
    string? ShortDescription,
    string? FullDescription,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryPath,
    IReadOnlyDictionary<string, object?> Attributes,
    IReadOnlyCollection<string> Tags,
    IReadOnlyDictionary<string, object?> Seo,
    IReadOnlyDictionary<Guid, string?> ImageAltText,
    IReadOnlyCollection<string> MissingFields,
    IReadOnlyCollection<string> RiskFlags,
    decimal QualityScore);
