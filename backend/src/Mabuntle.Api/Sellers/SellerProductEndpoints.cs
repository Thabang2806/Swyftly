using System.Security.Claims;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Mabuntle.Api.Catalog;
using Mabuntle.Api.Results;
using Mabuntle.Api.Security;
using Mabuntle.Application.Abstractions;
using Mabuntle.Application.Ai;
using Mabuntle.Application.Catalog;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Media;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Media;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerProductEndpoints
{
    private const int MaxRevisionImages = 12;
    private const int MaxVariantRevisionImportRows = 500;

    private static readonly string[] VariantRevisionCsvHeaders =
    [
        "operation",
        "sourceVariantId",
        "sku",
        "size",
        "colour",
        "price",
        "compareAtPrice",
        "initialStockQuantity",
        "barcode"
    ];

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
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapPost("", CreateProductAsync)
            .WithName("CreateSellerProduct")
            .WithSummary("Creates a seller-owned product draft.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.ProductWrite)
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
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .RequireRateLimiting(MabuntleRateLimitPolicies.ProductWrite)
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
            .RequireRateLimiting(MabuntleRateLimitPolicies.Ai)
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
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .RequireRateLimiting(MabuntleRateLimitPolicies.ProductWrite)
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

        group.MapGet("/{id:guid}/variant-revision", GetVariantRevisionAsync)
            .WithName("GetSellerProductVariantRevision")
            .WithSummary("Returns or creates the active seller variant and pricing revision for a published product.")
            .Produces<SellerProductVariantRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/variant-revision", UpdateVariantRevisionAsync)
            .WithName("UpdateSellerProductVariantRevision")
            .WithSummary("Stages variant and pricing changes for a published product.")
            .Produces<SellerProductVariantRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/variant-revision/export.csv", ExportVariantRevisionCsvAsync)
            .WithName("ExportSellerProductVariantRevisionCsv")
            .WithSummary("Exports current live variants for bulk published variant revision staging.")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/variant-revision/import-template.csv", ExportVariantRevisionImportTemplateCsvAsync)
            .WithName("ExportSellerProductVariantRevisionImportTemplateCsv")
            .WithSummary("Exports a CSV template for bulk published variant revision staging.")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/variant-revision/import/preview", PreviewVariantRevisionImportAsync)
            .WithName("PreviewSellerProductVariantRevisionImport")
            .WithSummary("Previews a published variant revision CSV import without changing data.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<SellerProductVariantRevisionBulkImportResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        group.MapPost("/{id:guid}/variant-revision/bulk-stage", BulkStageVariantRevisionAsync)
            .WithName("BulkStageSellerProductVariantRevision")
            .WithSummary("Replaces draft staged variant revision items with validated bulk rows.")
            .Produces<SellerProductVariantRevisionBulkImportResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/variant-revision/submit-review", SubmitVariantRevisionReviewAsync)
            .WithName("SubmitSellerProductVariantRevision")
            .WithSummary("Submits a published product variant and pricing revision for admin review.")
            .Produces<SellerProductVariantRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/variant-revision/cancel", CancelVariantRevisionAsync)
            .WithName("CancelSellerProductVariantRevision")
            .WithSummary("Cancels the active published product variant and pricing revision.")
            .Produces<SellerProductVariantRevisionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateProductAsync(
        UpsertSellerProductRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
                request.FullDescription,
                request.SeoTitle,
                request.SeoDescription,
                request.MerchandisingLabel,
                request.CareInstructions,
                request.ProductDisclaimer);
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
        MabuntleDbContext dbContext,
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
                product.MerchandisingLabel,
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                dbContext.ProductImages
                    .Where(image => image.ProductId == product.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.AltText)
                    .FirstOrDefault(),
                dbContext.ProductVariants
                    .Where(variant => variant.ProductId == product.Id)
                    .Sum(variant => (int?)variant.StockQuantity) ?? 0,
                dbContext.ProductVariants
                    .Where(variant => variant.ProductId == product.Id)
                    .Sum(variant => (int?)variant.ReservedQuantity) ?? 0,
                dbContext.ProductVariants
                    .Where(variant => variant.ProductId == product.Id)
                    .Sum(variant => (int?)(variant.StockQuantity - variant.ReservedQuantity)) ?? 0,
                dbContext.ProductVariants
                    .Count(variant => variant.ProductId == product.Id
                        && variant.Status == ProductVariantStatus.Active
                        && variant.StockQuantity > variant.ReservedQuantity
                        && variant.StockQuantity - variant.ReservedQuantity <= 5),
                dbContext.ProductVariants
                    .Count(variant => variant.ProductId == product.Id
                        && (variant.Status == ProductVariantStatus.OutOfStock
                            || variant.StockQuantity <= variant.ReservedQuantity)),
                product.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(products);
    }

    private static async Task<IResult> GetProductAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
                request.FullDescription,
                request.SeoTitle,
                request.SeoDescription,
                request.MerchandisingLabel,
                request.CareInstructions,
                request.ProductDisclaimer);
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        IFormFile file,
        [FromForm] string? altText,
        [FromForm] int sortOrder,
        [FromForm] bool isPrimary,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
                SerializeAttributes(attributes),
                request.SeoTitle,
                request.SeoDescription,
                request.MerchandisingLabel,
                request.CareInstructions,
                request.ProductDisclaimer);
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
        IFormFile file,
        [FromForm] string? altText,
        [FromForm] int sortOrder,
        [FromForm] bool isPrimary,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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

    private static async Task<IResult> GetVariantRevisionAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
            return Validation("product", "Variant and pricing revisions are available only for published products.");
        }

        var revision = await GetOrCreateActiveVariantRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateVariantRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateVariantRevisionAsync(
        Guid id,
        UpsertSellerProductVariantRevisionRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
            return Validation("product", "Variant and pricing revisions can be staged only for published products.");
        }

        var revision = await GetOrCreateActiveVariantRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        if (!revision.CanSellerEdit)
        {
            return Validation("revision", "A variant revision already submitted for review cannot be edited.");
        }

        var currentVariants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id)
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);
        var proposedItems = new List<ProductVariantRevisionItem>();
        foreach (var requestItem in request.Items ?? [])
        {
            var itemResult = TryCreateVariantRevisionItem(revision.Id, requestItem, currentVariants);
            if (itemResult.Error is not null)
            {
                return Validation("items", itemResult.Error);
            }

            proposedItems.Add(itemResult.Item!);
        }

        var validation = await ProductVariantRevisionRules.ValidateAsync(
            product.Id,
            proposedItems,
            dbContext,
            cancellationToken);
        if (!validation.IsValid)
        {
            return HttpResults.ValidationProblem(validation.Errors);
        }

        var existingItems = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        dbContext.ProductVariantRevisionItems.RemoveRange(existingItems);
        dbContext.ProductVariantRevisionItems.AddRange(proposedItems);

        try
        {
            revision.UpdateSellerReason(request.SellerReason);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("revision", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateVariantRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> ExportVariantRevisionCsvAsync(
        Guid id,
        ClaimsPrincipal principal,
        HttpResponse response,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Variant revision export is available only for published products.");
        }

        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == product.Id)
            .OrderBy(variant => variant.Sku)
            .ToListAsync(cancellationToken);

        response.Headers.ContentDisposition = $"attachment; filename=\"{product.Slug ?? product.Id.ToString()}-variant-revision-export.csv\"";
        return HttpResults.Text(BuildVariantRevisionCsv(variants), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> ExportVariantRevisionImportTemplateCsvAsync(
        Guid id,
        ClaimsPrincipal principal,
        HttpResponse response,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Variant revision import templates are available only for published products.");
        }

        response.Headers.ContentDisposition = $"attachment; filename=\"{product.Slug ?? product.Id.ToString()}-variant-revision-template.csv\"";
        return HttpResults.Text(BuildVariantRevisionCsv(Array.Empty<ProductVariant>()), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> PreviewVariantRevisionImportAsync(
        Guid id,
        IFormFile file,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await GetOwnedProductAsync(id, principal, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        if (product.Status != ProductStatus.Published)
        {
            return Validation("product", "Variant revision imports are available only for published products.");
        }

        if (file.Length == 0)
        {
            return Validation("file", "Variant revision import CSV cannot be empty.");
        }

        IReadOnlyCollection<VariantRevisionImportRow> rows;
        try
        {
            rows = await ParseVariantRevisionCsvAsync(file, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("file", exception.Message);
        }

        if (rows.Count > MaxVariantRevisionImportRows)
        {
            return Validation("file", $"Variant revision import cannot contain more than {MaxVariantRevisionImportRows} rows.");
        }

        var preview = await BuildVariantRevisionBulkPreviewAsync(
            product.Id,
            Guid.NewGuid(),
            rows,
            dbContext,
            cancellationToken);

        return HttpResults.Ok(preview.Response);
    }

    private static async Task<IResult> BulkStageVariantRevisionAsync(
        Guid id,
        BulkStageSellerProductVariantRevisionRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
            return Validation("product", "Variant revision bulk staging is available only for published products.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Validation("items", "At least one variant revision row is required.");
        }

        if (request.Items.Count > MaxVariantRevisionImportRows)
        {
            return Validation("items", $"Variant revision bulk staging cannot contain more than {MaxVariantRevisionImportRows} rows.");
        }

        var revision = await GetOrCreateActiveVariantRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        if (!revision.CanSellerEdit)
        {
            return Validation("revision", "A variant revision already submitted for review cannot be edited.");
        }

        var rows = request.Items
            .Select((item, index) => VariantRevisionImportRow.FromRequest(index + 1, item))
            .ToArray();
        var preview = await BuildVariantRevisionBulkPreviewAsync(
            product.Id,
            revision.Id,
            rows,
            dbContext,
            cancellationToken);

        if (preview.Response.ErrorRows > 0)
        {
            return HttpResults.BadRequest(preview.Response);
        }

        var existingItems = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        dbContext.ProductVariantRevisionItems.RemoveRange(existingItems);
        if (preview.ChangedItems.Count > 0)
        {
            dbContext.ProductVariantRevisionItems.AddRange(preview.ChangedItems);
        }

        try
        {
            revision.UpdateSellerReason(request.SellerReason);
        }
        catch (InvalidOperationException exception)
        {
            return Validation("revision", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(preview.Response);
    }

    private static async Task<IResult> SubmitVariantRevisionReviewAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
            return Validation("product", "Only published products can submit variant and pricing revisions.");
        }

        var revision = await GetActiveVariantRevisionAsync(product.Id, dbContext, cancellationToken);
        if (revision is null)
        {
            return Validation("revision", "Create a variant revision before submitting it for review.");
        }

        var items = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        var validation = await ProductVariantRevisionRules.ValidateAsync(product.Id, items, dbContext, cancellationToken);
        if (!validation.IsValid)
        {
            return HttpResults.ValidationProblem(validation.Errors);
        }

        try
        {
            revision.SubmitForReview(items.Count > 0, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Validation("revision", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateVariantRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> CancelVariantRevisionAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
            return Validation("product", "Only published products can cancel variant and pricing revisions.");
        }

        var revision = await GetOrCreateActiveVariantRevisionAsync(product, dbContext, timeProvider, cancellationToken);
        revision.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateVariantRevisionResponseAsync(revision.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> GenerateAiSuggestionAsync(
        Guid id,
        GenerateSellerAiSuggestionRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
                fullDescription,
                product.SeoTitle,
                product.SeoDescription,
                product.MerchandisingLabel,
                product.CareInstructions,
                product.ProductDisclaimer);
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
            await BuildProductAttributesJsonAsync(product.Id, dbContext, cancellationToken),
            product.SeoTitle,
            product.SeoDescription,
            product.MerchandisingLabel,
            product.CareInstructions,
            product.ProductDisclaimer);
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

    private static async Task<VariantRevisionBulkPreviewBuildResult> BuildVariantRevisionBulkPreviewAsync(
        Guid productId,
        Guid revisionId,
        IReadOnlyCollection<VariantRevisionImportRow> rows,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentVariants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == productId)
            .OrderBy(variant => variant.Sku)
            .ToListAsync(cancellationToken);
        var byId = currentVariants.ToDictionary(variant => variant.Id);
        var bySku = currentVariants
            .GroupBy(variant => variant.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var byBarcode = currentVariants
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Barcode))
            .GroupBy(variant => variant.Barcode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var seenSourceIds = new HashSet<Guid>();
        var responseRows = new List<SellerProductVariantRevisionBulkImportRowResponse>();
        var changedItems = new List<ProductVariantRevisionItem>();

        foreach (var row in rows)
        {
            var messages = row.ParseMessages.ToList();
            ProductVariantRevisionItemOperation? operation = null;
            ProductVariant? source = null;
            ProductVariantRevisionItem? item = null;

            if (!Enum.TryParse<ProductVariantRevisionItemOperation>(row.Operation, ignoreCase: true, out var parsedOperation)
                || !Enum.IsDefined(parsedOperation))
            {
                messages.Add("Operation must be Add, Update, or Deactivate.");
            }
            else
            {
                operation = parsedOperation;
            }

            if (operation == ProductVariantRevisionItemOperation.Add)
            {
                if (row.SourceVariantId.HasValue)
                {
                    messages.Add("Add rows cannot include a source variant id.");
                }
            }
            else if (operation.HasValue)
            {
                source = ResolveVariantRevisionImportSource(row, byId, bySku, byBarcode, messages);
                if (source is not null && !seenSourceIds.Add(source.Id))
                {
                    messages.Add("This source variant appears more than once in the import.");
                }
            }

            if (operation.HasValue && messages.Count == 0)
            {
                item = TryCreateVariantRevisionImportItem(revisionId, operation.Value, row, source, messages);
            }

            var rowStatus = InventoryImportRowStatus.Error;
            if (messages.Count == 0 && item is not null)
            {
                rowStatus = IsVariantRevisionImportChanged(item, source)
                    ? InventoryImportRowStatus.Changed
                    : InventoryImportRowStatus.Unchanged;

                if (rowStatus == InventoryImportRowStatus.Changed)
                {
                    changedItems.Add(item);
                }
            }

            responseRows.Add(CreateVariantRevisionImportRowResponse(row, operation, source, item, rowStatus, messages));
        }

        ProductVariantRevisionValidationResult validation;
        if (changedItems.Count == 0)
        {
            validation = await ProductVariantRevisionRules.ValidateAsync(
                productId,
                Array.Empty<ProductVariantRevisionItem>(),
                dbContext,
                cancellationToken);
        }
        else
        {
            validation = await ProductVariantRevisionRules.ValidateAsync(
                productId,
                changedItems,
                dbContext,
                cancellationToken);
        }

        if (changedItems.Count > 0 && !validation.IsValid)
        {
            var finalSetMessages = validation.Errors
                .SelectMany(item => item.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(message => $"Proposed final variant set is invalid: {message}")
                .ToArray();
            responseRows = responseRows
                .Select(row => row.RowStatus == InventoryImportRowStatus.Error
                    ? row
                    : row with
                    {
                        RowStatus = InventoryImportRowStatus.Error,
                        ValidationMessages = row.ValidationMessages.Concat(finalSetMessages).ToArray()
                    })
                .ToList();
        }

        var errorRows = responseRows.Count(row => row.RowStatus == InventoryImportRowStatus.Error);
        var changedRows = responseRows.Count(row => row.RowStatus == InventoryImportRowStatus.Changed);
        var unchangedRows = responseRows.Count(row => row.RowStatus == InventoryImportRowStatus.Unchanged);
        IReadOnlyCollection<SellerProductVariantRevisionFinalVariantResponse> finalVariants =
            changedItems.Count == 0 || !validation.IsValid
                ? Array.Empty<SellerProductVariantRevisionFinalVariantResponse>()
                : validation.FinalVariants
                    .Select(MapVariantRevisionFinalVariantResponse)
                    .ToArray();

        return new VariantRevisionBulkPreviewBuildResult(
            new SellerProductVariantRevisionBulkImportResponse(
                responseRows.Count,
                responseRows.Count - errorRows,
                errorRows,
                changedRows,
                unchangedRows,
                responseRows,
                finalVariants),
            errorRows == 0 ? changedItems : Array.Empty<ProductVariantRevisionItem>());
    }

    private static ProductVariant? ResolveVariantRevisionImportSource(
        VariantRevisionImportRow row,
        IReadOnlyDictionary<Guid, ProductVariant> byId,
        IReadOnlyDictionary<string, ProductVariant[]> bySku,
        IReadOnlyDictionary<string, ProductVariant[]> byBarcode,
        List<string> messages)
    {
        if (row.SourceVariantId.HasValue)
        {
            if (!byId.TryGetValue(row.SourceVariantId.Value, out var idSource))
            {
                messages.Add("Source variant id was not found for this product.");
                return null;
            }

            var skuSourceForId = ResolveUniqueVariant("SKU", row.Sku?.Trim(), bySku, messages);
            var barcodeSourceForId = ResolveUniqueVariant("Barcode", row.Barcode?.Trim(), byBarcode, messages);
            if (skuSourceForId is not null && skuSourceForId.Id != idSource.Id)
            {
                messages.Add("sourceVariantId and SKU refer to different current variants.");
            }

            if (barcodeSourceForId is not null && barcodeSourceForId.Id != idSource.Id)
            {
                messages.Add("sourceVariantId and barcode refer to different current variants.");
            }

            return idSource;
        }

        var sku = row.Sku?.Trim();
        var barcode = row.Barcode?.Trim();
        var skuSource = ResolveUniqueVariant("SKU", sku, bySku, messages);
        var barcodeSource = ResolveUniqueVariant("Barcode", barcode, byBarcode, messages);

        if (skuSource is not null && barcodeSource is not null && skuSource.Id != barcodeSource.Id)
        {
            messages.Add("SKU and barcode refer to different current variants.");
            return null;
        }

        var source = barcodeSource ?? skuSource;
        if (source is null)
        {
            messages.Add("Update and Deactivate rows require sourceVariantId, current SKU, or current barcode.");
        }

        return source;
    }

    private static ProductVariant? ResolveUniqueVariant(
        string label,
        string? value,
        IReadOnlyDictionary<string, ProductVariant[]> lookup,
        List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!lookup.TryGetValue(value, out var matches))
        {
            return null;
        }

        if (matches.Length > 1)
        {
            messages.Add($"{label} matches multiple current variants; use sourceVariantId for this row.");
            return null;
        }

        return matches[0];
    }

    private static ProductVariantRevisionItem? TryCreateVariantRevisionImportItem(
        Guid revisionId,
        ProductVariantRevisionItemOperation operation,
        VariantRevisionImportRow row,
        ProductVariant? source,
        List<string> messages)
    {
        var sku = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Sku
            : DefaultBlank(row.Sku, source?.Sku);
        var size = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Size
            : DefaultBlank(row.Size, source?.Size);
        var colour = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Colour
            : DefaultBlank(row.Colour, source?.Colour);
        var price = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Price
            : row.Price ?? source?.Price;
        var compareAtPrice = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.CompareAtPrice
            : row.CompareAtPrice ?? source?.CompareAtPrice;
        var initialStockQuantity = operation == ProductVariantRevisionItemOperation.Add
            ? row.InitialStockQuantity
            : null;
        var barcode = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Barcode
            : DefaultBlank(row.Barcode, source?.Barcode);

        if (operation == ProductVariantRevisionItemOperation.Add && row.InitialStockQuantity is null)
        {
            messages.Add("Add rows require initialStockQuantity.");
        }

        if (price is null)
        {
            messages.Add("Price is required.");
        }

        if (messages.Count > 0)
        {
            return null;
        }

        try
        {
            return new ProductVariantRevisionItem(
                revisionId,
                operation,
                operation == ProductVariantRevisionItemOperation.Add ? null : source!.Id,
                sku ?? string.Empty,
                size ?? string.Empty,
                colour ?? string.Empty,
                price!.Value,
                compareAtPrice,
                initialStockQuantity,
                operation == ProductVariantRevisionItemOperation.Deactivate
                    ? ProductVariantStatus.Inactive
                    : source?.Status ?? ProductVariantStatus.Active,
                barcode);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            messages.Add(exception.Message);
            return null;
        }
    }

    private static SellerProductVariantRevisionBulkImportRowResponse CreateVariantRevisionImportRowResponse(
        VariantRevisionImportRow row,
        ProductVariantRevisionItemOperation? operation,
        ProductVariant? source,
        ProductVariantRevisionItem? item,
        string rowStatus,
        IReadOnlyCollection<string> messages) =>
        new(
            row.RowNumber,
            operation?.ToString() ?? row.Operation,
            source?.Id ?? row.SourceVariantId,
            source?.Sku,
            source?.Size,
            source?.Colour,
            source?.Price,
            source?.CompareAtPrice,
            source?.Status.ToString(),
            source?.Barcode,
            item?.Sku ?? row.Sku,
            item?.Size ?? row.Size,
            item?.Colour ?? row.Colour,
            item?.Price ?? row.Price,
            item?.CompareAtPrice ?? row.CompareAtPrice,
            item?.InitialStockQuantity ?? row.InitialStockQuantity,
            item?.Barcode ?? row.Barcode,
            rowStatus,
            messages);

    private static bool IsVariantRevisionImportChanged(
        ProductVariantRevisionItem item,
        ProductVariant? source)
    {
        if (item.Operation == ProductVariantRevisionItemOperation.Add)
        {
            return true;
        }

        if (source is null)
        {
            return false;
        }

        if (item.Operation == ProductVariantRevisionItemOperation.Deactivate)
        {
            return source.Status != ProductVariantStatus.Inactive;
        }

        return !string.Equals(item.Sku, source.Sku, StringComparison.Ordinal)
            || !string.Equals(item.Size, source.Size, StringComparison.Ordinal)
            || !string.Equals(item.Colour, source.Colour, StringComparison.Ordinal)
            || item.Price != source.Price
            || item.CompareAtPrice != source.CompareAtPrice
            || !string.Equals(item.Barcode, source.Barcode, StringComparison.Ordinal)
            || item.ProposedStatus != source.Status;
    }

    private static SellerProductVariantRevisionFinalVariantResponse MapVariantRevisionFinalVariantResponse(
        ProductVariantRevisionFinalVariant variant) =>
        new(
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
            variant.AvailableQuantity);

    private static string? DefaultBlank(string? value, string? defaultValue) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

    private static string BuildVariantRevisionCsv(IReadOnlyCollection<ProductVariant> variants)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", VariantRevisionCsvHeaders.Select(Csv)));

        foreach (var variant in variants)
        {
            AppendCsvLine(
                builder,
                "Update",
                variant.Id.ToString(),
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price.ToString(CultureInfo.InvariantCulture),
                variant.CompareAtPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                string.Empty,
                variant.Barcode ?? string.Empty);
        }

        return builder.ToString();
    }

    private static async Task<IReadOnlyCollection<VariantRevisionImportRow>> ParseVariantRevisionCsvAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var records = ParseCsv(content);

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Variant revision import CSV must include a header row.");
        }

        var headers = records[0]
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .Where(item => item.Header.Length > 0)
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        if (!headers.ContainsKey("operation"))
        {
            throw new InvalidOperationException("Variant revision import CSV is missing the 'operation' column.");
        }

        var rows = new List<VariantRevisionImportRow>();
        var rowNumber = 1;
        foreach (var record in records.Skip(1))
        {
            rowNumber++;
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var messages = new List<string>();
            var sourceVariantIdText = ReadColumn(record, headers, "sourceVariantId");
            var priceText = ReadColumn(record, headers, "price");
            var compareAtPriceText = ReadColumn(record, headers, "compareAtPrice");
            var initialStockText = ReadColumn(record, headers, "initialStockQuantity");

            rows.Add(new VariantRevisionImportRow(
                rowNumber,
                ReadColumn(record, headers, "operation") ?? string.Empty,
                ParseGuidOrNull(sourceVariantIdText, "sourceVariantId", messages),
                ReadColumn(record, headers, "sku"),
                ReadColumn(record, headers, "size"),
                ReadColumn(record, headers, "colour"),
                ParseDecimalOrNull(priceText, "price", messages),
                ParseDecimalOrNull(compareAtPriceText, "compareAtPrice", messages),
                ParseIntOrNull(initialStockText, "initialStockQuantity", messages),
                ReadColumn(record, headers, "barcode"),
                messages));
        }

        return rows;
    }

    private static Guid? ParseGuidOrNull(string? value, string fieldName, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var parsed))
        {
            return parsed;
        }

        messages.Add($"{fieldName} must be a valid GUID.");
        return null;
    }

    private static decimal? ParseDecimalOrNull(string? value, string fieldName, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        messages.Add($"{fieldName} must be a decimal number.");
        return null;
    }

    private static int? ParseIntOrNull(string? value, string fieldName, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        messages.Add($"{fieldName} must be an integer.");
        return null;
    }

    private static List<string[]> ParseCsv(string content)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == ',')
            {
                row.Add(value.ToString());
                value.Clear();
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n'))
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(value.ToString());
                value.Clear();
                rows.Add(row.ToArray());
                row.Clear();
                continue;
            }

            value.Append(character);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Variant revision import CSV contains an unterminated quoted value.");
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string? ReadColumn(
        IReadOnlyList<string> record,
        IReadOnlyDictionary<string, int> headers,
        string header)
    {
        if (!headers.TryGetValue(header, out var index) || index >= record.Count)
        {
            return null;
        }

        return record[index].Trim();
    }

    private static void AppendCsvLine(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(Csv)));
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static async Task<ProductVariantRevision?> GetActiveVariantRevisionAsync(
        Guid productId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.ProductVariantRevisions
            .Where(revision => revision.ProductId == productId
                && (revision.Status == ProductVariantRevisionStatus.Draft
                    || revision.Status == ProductVariantRevisionStatus.PendingReview
                    || revision.Status == ProductVariantRevisionStatus.Rejected))
            .OrderByDescending(revision => revision.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<ProductVariantRevision> GetOrCreateActiveVariantRevisionAsync(
        Product product,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var revision = await GetActiveVariantRevisionAsync(product.Id, dbContext, cancellationToken);
        if (revision is not null)
        {
            return revision;
        }

        revision = new ProductVariantRevision(product.Id, product.SellerId);
        dbContext.ProductVariantRevisions.Add(revision);
        return revision;
    }

    private static (ProductVariantRevisionItem? Item, string? Error) TryCreateVariantRevisionItem(
        Guid revisionId,
        UpsertSellerProductVariantRevisionItemRequest request,
        IReadOnlyDictionary<Guid, ProductVariant> currentVariants)
    {
        if (!Enum.TryParse<ProductVariantRevisionItemOperation>(request.Operation, ignoreCase: true, out var operation))
        {
            return (null, "Item operation must be Add, Update, or Deactivate.");
        }

        ProductVariant? source = null;
        if (operation != ProductVariantRevisionItemOperation.Add)
        {
            if (!request.SourceVariantId.HasValue ||
                !currentVariants.TryGetValue(request.SourceVariantId.Value, out source))
            {
                return (null, "Existing variant changes require a source variant owned by this product.");
            }
        }

        var sku = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Sku
            : request.Sku;
        var size = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Size
            : request.Size;
        var colour = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Colour
            : request.Colour;
        var price = operation == ProductVariantRevisionItemOperation.Deactivate
            ? source!.Price
            : request.Price;

        if (price is null)
        {
            return (null, "Price is required for staged variant additions and updates.");
        }

        try
        {
            return (new ProductVariantRevisionItem(
                revisionId,
                operation,
                operation == ProductVariantRevisionItemOperation.Add ? null : request.SourceVariantId,
                sku ?? string.Empty,
                size ?? string.Empty,
                colour ?? string.Empty,
                price.Value,
                operation == ProductVariantRevisionItemOperation.Deactivate ? source!.CompareAtPrice : request.CompareAtPrice,
                operation == ProductVariantRevisionItemOperation.Add ? request.InitialStockQuantity : null,
                operation == ProductVariantRevisionItemOperation.Deactivate
                    ? ProductVariantStatus.Inactive
                    : source?.Status ?? ProductVariantStatus.Active,
                operation == ProductVariantRevisionItemOperation.Deactivate ? source!.Barcode : request.Barcode), null);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return (null, exception.Message);
        }
    }

    private static async Task ClearPrimaryRevisionImagesAsync(
        Guid revisionId,
        Guid? exceptImageId,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        var moderationEvents = await GetSellerModerationEventsAsync(
            "ProductListingRevision",
            revision.Id,
            dbContext,
            cancellationToken);

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
            revision.SeoTitle,
            revision.SeoDescription,
            revision.MerchandisingLabel,
            revision.CareInstructions,
            revision.ProductDisclaimer,
            ReadStringArray(revision.TagsJson),
            ReadRevisionAttributes(revision.AttributesJson),
            images,
            moderationEvents);
    }

    private static async Task<SellerProductVariantRevisionResponse> CreateVariantRevisionResponseAsync(
        Guid revisionId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ProductVariantRevisions.SingleAsync(
            item => item.Id == revisionId,
            cancellationToken);
        var currentVariants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == revision.ProductId)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new SellerProductVariantRevisionFinalVariantResponse(
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
        var items = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .OrderBy(item => item.Operation)
            .ThenBy(item => item.Size)
            .ThenBy(item => item.Colour)
            .Select(item => new SellerProductVariantRevisionItemResponse(
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
            .ToListAsync(cancellationToken);
        var rawItems = await dbContext.ProductVariantRevisionItems
            .Where(item => item.RevisionId == revision.Id)
            .ToListAsync(cancellationToken);
        var validation = await ProductVariantRevisionRules.ValidateAsync(
            revision.ProductId,
            rawItems,
            dbContext,
            cancellationToken);
        var proposedFinalVariants = validation.FinalVariants
            .Select(variant => new SellerProductVariantRevisionFinalVariantResponse(
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
        var moderationEvents = await GetSellerModerationEventsAsync(
            "ProductVariantRevision",
            revision.Id,
            dbContext,
            cancellationToken);

        return new SellerProductVariantRevisionResponse(
            revision.Id,
            revision.ProductId,
            revision.SellerId,
            revision.Status.ToString(),
            revision.CanSellerEdit,
            revision.SellerReason,
            revision.RejectionReason,
            revision.SubmittedAtUtc,
            revision.ReviewedAtUtc,
            currentVariants,
            items,
            proposedFinalVariants,
            moderationEvents);
    }

    private static async Task<IResult?> ValidateCategoryAsync(
        Guid? categoryId,
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext)
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        MabuntleDbContext dbContext,
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
        var moderationEvents = await GetSellerModerationEventsAsync(
            "Product",
            product.Id,
            dbContext,
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
            moderationEvents);
    }

    private static async Task<IReadOnlyCollection<SellerModerationEventResponse>> GetSellerModerationEventsAsync(
        string entityType,
        Guid entityId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog => auditLog.EntityType == entityType && auditLog.EntityId == entityId.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new SellerModerationEventResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

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
    string? SeoTitle,
    string? SeoDescription,
    string? MerchandisingLabel,
    string? CareInstructions,
    string? ProductDisclaimer,
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
    string? SeoTitle,
    string? SeoDescription,
    string? MerchandisingLabel,
    string? CareInstructions,
    string? ProductDisclaimer,
    IReadOnlyCollection<string>? Tags,
    IReadOnlyDictionary<string, JsonElement>? Attributes);

public sealed record UpsertSellerProductVariantRevisionRequest(
    string? SellerReason,
    IReadOnlyCollection<UpsertSellerProductVariantRevisionItemRequest>? Items);

public sealed record UpsertSellerProductVariantRevisionItemRequest(
    string Operation,
    Guid? SourceVariantId,
    string? Sku,
    string? Size,
    string? Colour,
    decimal? Price,
    decimal? CompareAtPrice,
    int? InitialStockQuantity,
    string? Barcode);

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
    string? MerchandisingLabel,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText,
    int TotalStockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int LowStockVariantCount,
    int OutOfStockVariantCount,
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
    IReadOnlyCollection<SellerProductVariantResponse> Variants,
    IReadOnlyCollection<SellerProductImageResponse> Images,
    IReadOnlyCollection<SellerModerationEventResponse> ModerationEvents);

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
    string? SeoTitle,
    string? SeoDescription,
    string? MerchandisingLabel,
    string? CareInstructions,
    string? ProductDisclaimer,
    IReadOnlyCollection<string> Tags,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<SellerProductRevisionImageResponse> Images,
    IReadOnlyCollection<SellerModerationEventResponse> ModerationEvents);

public sealed record SellerProductRevisionImageResponse(
    Guid RevisionImageId,
    Guid? SourceProductImageId,
    string Url,
    string StorageKey,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset CreatedAtUtc);

public sealed record SellerProductVariantRevisionResponse(
    Guid RevisionId,
    Guid ProductId,
    Guid SellerId,
    string Status,
    bool CanEdit,
    string? SellerReason,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    IReadOnlyCollection<SellerProductVariantRevisionFinalVariantResponse> CurrentVariants,
    IReadOnlyCollection<SellerProductVariantRevisionItemResponse> Items,
    IReadOnlyCollection<SellerProductVariantRevisionFinalVariantResponse> ProposedFinalVariants,
    IReadOnlyCollection<SellerModerationEventResponse> ModerationEvents);

public sealed record SellerProductVariantRevisionItemResponse(
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

public sealed record SellerProductVariantRevisionFinalVariantResponse(
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

public sealed record BulkStageSellerProductVariantRevisionRequest(
    string? SellerReason,
    IReadOnlyCollection<UpsertSellerProductVariantRevisionItemRequest>? Items);

public sealed record SellerProductVariantRevisionBulkImportResponse(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int ChangedRows,
    int UnchangedRows,
    IReadOnlyCollection<SellerProductVariantRevisionBulkImportRowResponse> Rows,
    IReadOnlyCollection<SellerProductVariantRevisionFinalVariantResponse> ProposedFinalVariants);

public sealed record SellerProductVariantRevisionBulkImportRowResponse(
    int RowNumber,
    string Operation,
    Guid? SourceVariantId,
    string? CurrentSku,
    string? CurrentSize,
    string? CurrentColour,
    decimal? CurrentPrice,
    decimal? CurrentCompareAtPrice,
    string? CurrentStatus,
    string? CurrentBarcode,
    string? ProposedSku,
    string? ProposedSize,
    string? ProposedColour,
    decimal? ProposedPrice,
    decimal? ProposedCompareAtPrice,
    int? ProposedInitialStockQuantity,
    string? ProposedBarcode,
    string RowStatus,
    IReadOnlyCollection<string> ValidationMessages);

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

internal sealed record VariantRevisionBulkPreviewBuildResult(
    SellerProductVariantRevisionBulkImportResponse Response,
    IReadOnlyCollection<ProductVariantRevisionItem> ChangedItems);

internal sealed record VariantRevisionImportRow(
    int RowNumber,
    string Operation,
    Guid? SourceVariantId,
    string? Sku,
    string? Size,
    string? Colour,
    decimal? Price,
    decimal? CompareAtPrice,
    int? InitialStockQuantity,
    string? Barcode,
    IReadOnlyCollection<string> ParseMessages)
{
    public static VariantRevisionImportRow FromRequest(
        int rowNumber,
        UpsertSellerProductVariantRevisionItemRequest request) =>
        new(
            rowNumber,
            request.Operation,
            request.SourceVariantId,
            request.Sku,
            request.Size,
            request.Colour,
            request.Price,
            request.CompareAtPrice,
            request.InitialStockQuantity,
            request.Barcode,
            []);
}
