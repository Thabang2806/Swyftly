using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Results;
using Mabuntle.Api.Security;
using Mabuntle.Application.Abstractions;
using Mabuntle.Application.Ai;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Ai;

public static class BuyerVisualSearchEndpoints
{
    public static IEndpointRouteBuilder MapBuyerVisualSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/buyer/ai/visual-search", SearchAsync)
            .WithTags("Buyer AI")
            .WithName("SearchWithVisualSearch")
            .WithSummary("Extracts visual attributes from a transient image input and returns real published product matches only.")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly)
            .RequireRateLimiting(MabuntleRateLimitPolicies.Ai)
            .Produces<BuyerVisualSearchResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        BuyerVisualSearchRequest request,
        ClaimsPrincipal principal,
        IAiVisualSearchService visualSearchService,
        ICurrentUser currentUser,
        IBuyerAiPersonalizationService personalizationService,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var attributesResult = await visualSearchService.ExtractAttributesAsync(
            new VisualSearchExtractionRequest(
                request.ImageReference,
                request.ImageDataBase64,
                request.FileName,
                request.ContentType,
                currentUser.UserId),
            cancellationToken);
        if (attributesResult.IsFailure)
        {
            return attributesResult.ToHttpResult(HttpResults.Ok);
        }

        var attributes = attributesResult.Value;
        var candidates = await FindCandidatesAsync(attributes, dbContext, cancellationToken);
        var products = candidates
            .Select(candidate => new BuyerVisualSearchProductCardResponse(
                candidate.Product.Id,
                candidate.Product.Title ?? "Untitled product",
                candidate.Product.Slug ?? candidate.Product.Id.ToString("N"),
                candidate.SellerDisplayName,
                candidate.PrimaryImageUrl,
                candidate.MinPrice,
                "ZAR",
                BuildMatchReasons(attributes, candidate),
                PersonalizationApplied: false,
                []))
            .ToArray();
        products = await ApplyPersonalizationAsync(
            products,
            ParseUserId(currentUser.UserId),
            personalizationService,
            timeProvider.GetUtcNow(),
            cancellationToken);

        await BuyerAiDiscoveryHistoryWriter.SaveVisualSearchHistoryIfEnabledAsync(
            principal,
            attributes,
            products,
            dbContext,
            timeProvider,
            cancellationToken);

        return HttpResults.Ok(new BuyerVisualSearchResponse(
            attributes,
            products,
            products.Length == 0
                ? "No published in-stock products matched the extracted visual attributes."
                : "These matches use extracted visual attributes against published Mabuntle products only.",
            "Uploaded image data is processed for this request only and is not persisted by the visual search MVP."));
    }

    private static async Task<IReadOnlyCollection<ProductCandidate>> FindCandidatesAsync(
        VisualSearchAttributes attributes,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Published)
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.Id).ToArray();
        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId) && variant.Status == ProductVariantStatus.Active && variant.StockQuantity > variant.ReservedQuantity)
            .ToListAsync(cancellationToken);
        var sellableProductIds = variants.Select(variant => variant.ProductId).Distinct().ToHashSet();
        var categoryIds = products
            .Where(product => product.CategoryId.HasValue)
            .Select(product => product.CategoryId!.Value)
            .Distinct()
            .ToArray();
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        var sellerIds = products.Select(product => product.SellerId).Distinct().ToArray();
        var storefronts = await dbContext.SellerStorefronts
            .AsNoTracking()
            .Where(storefront => sellerIds.Contains(storefront.SellerId))
            .ToDictionaryAsync(storefront => storefront.SellerId, storefront => storefront.StoreName, cancellationToken);
        var images = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId))
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ToListAsync(cancellationToken);
        var attributeValues = await dbContext.ProductAttributeValues
            .AsNoTracking()
            .Where(attribute => productIds.Contains(attribute.ProductId))
            .ToListAsync(cancellationToken);

        return products
            .Where(product => sellableProductIds.Contains(product.Id))
            .Select(product =>
            {
                var productVariants = variants.Where(variant => variant.ProductId == product.Id).ToArray();
                var categoryName = product.CategoryId.HasValue
                    ? categories.GetValueOrDefault(product.CategoryId.Value)
                    : null;

                return new ProductCandidate(
                    product,
                    categoryName,
                    storefronts.GetValueOrDefault(product.SellerId),
                    images.FirstOrDefault(image => image.ProductId == product.Id)?.Url,
                    productVariants.Min(variant => variant.Price),
                    productVariants.Select(variant => variant.Colour).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    attributeValues.Where(attribute => attribute.ProductId == product.Id).Select(attribute => attribute.ValueJson).ToArray());
            })
            .Where(candidate => MatchesAttributes(attributes, candidate))
            .OrderByDescending(candidate => Score(attributes, candidate))
            .ThenBy(candidate => candidate.MinPrice)
            .Take(8)
            .ToArray();
    }

    private static bool MatchesAttributes(VisualSearchAttributes attributes, ProductCandidate candidate)
    {
        var searchable = BuildSearchableText(candidate);
        return ContainsIfPresent(searchable, attributes.Category)
            && ContainsIfPresent(searchable, attributes.Style)
            && ContainsIfPresent(searchable, attributes.Shape)
            && ContainsIfPresent(searchable, attributes.Pattern)
            && ContainsIfPresent(searchable, attributes.MaterialGuess)
            && (attributes.Colour is null || candidate.VariantColours.Contains(attributes.Colour, StringComparer.OrdinalIgnoreCase) || searchable.Contains(attributes.Colour.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static int Score(VisualSearchAttributes attributes, ProductCandidate candidate)
    {
        var searchable = BuildSearchableText(candidate);
        var score = 0;

        if (attributes.Category is not null && string.Equals(candidate.CategoryName, attributes.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (attributes.Colour is not null && candidate.VariantColours.Contains(attributes.Colour, StringComparer.OrdinalIgnoreCase))
        {
            score += 3;
        }

        foreach (var value in new[] { attributes.Style, attributes.Shape, attributes.Pattern, attributes.MaterialGuess })
        {
            if (value is not null && searchable.Contains(value.ToLowerInvariant(), StringComparison.Ordinal))
            {
                score += 1;
            }
        }

        return score;
    }

    private static IReadOnlyCollection<string> BuildMatchReasons(
        VisualSearchAttributes attributes,
        ProductCandidate candidate)
    {
        var reasons = new List<string>();
        if (attributes.Category is not null && string.Equals(candidate.CategoryName, attributes.Category, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"Matches visual category {attributes.Category}.");
        }

        if (attributes.Colour is not null && candidate.VariantColours.Contains(attributes.Colour, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"Available in {attributes.Colour}.");
        }

        if (attributes.Style is not null)
        {
            reasons.Add($"Matches {attributes.Style.ToLowerInvariant()} styling cues.");
        }

        if (attributes.MaterialGuess is not null)
        {
            reasons.Add($"Material is only a low-confidence visual guess: {attributes.MaterialGuess}.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add($"Matched extracted visual search text for {candidate.Product.Title ?? "this product"}.");
        }

        return reasons;
    }

    private static string BuildSearchableText(ProductCandidate candidate) =>
        $"{candidate.Product.Title} {candidate.Product.ShortDescription} {candidate.Product.FullDescription} {candidate.CategoryName} {string.Join(' ', candidate.AttributeValues)}".ToLowerInvariant();

    private static bool ContainsIfPresent(string searchable, string? value) =>
        value is null || searchable.Contains(value.ToLowerInvariant(), StringComparison.Ordinal);

    private static Guid ParseUserId(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

    private static async Task<BuyerVisualSearchProductCardResponse[]> ApplyPersonalizationAsync(
        BuyerVisualSearchProductCardResponse[] products,
        Guid userId,
        IBuyerAiPersonalizationService personalizationService,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (products.Length == 0)
        {
            return products;
        }

        var personalization = await personalizationService.PersonalizeAsync(
            userId,
            products.Select(product => product.ProductId).ToArray(),
            now,
            cancellationToken);
        if (personalization.Count == 0)
        {
            return products;
        }

        var byProductId = personalization.ToDictionary(item => item.ProductId);
        return products
            .Select((product, index) =>
            {
                byProductId.TryGetValue(product.ProductId, out var result);
                return new
                {
                    Product = result is null
                        ? product
                        : product with
                        {
                            PersonalizationApplied = result.PersonalizationApplied,
                            PersonalizationReasons = result.PersonalizationReasons
                        },
                    Score = result?.Score ?? 0,
                    Index = index
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Select(item => item.Product)
            .ToArray();
    }

    private sealed record ProductCandidate(
        Product Product,
        string? CategoryName,
        string? SellerDisplayName,
        string? PrimaryImageUrl,
        decimal MinPrice,
        IReadOnlyCollection<string> VariantColours,
        IReadOnlyCollection<string> AttributeValues);
}

public sealed record BuyerVisualSearchRequest(
    string? ImageReference,
    string? ImageDataBase64,
    string? FileName,
    string? ContentType);

public sealed record BuyerVisualSearchResponse(
    VisualSearchAttributes Attributes,
    IReadOnlyCollection<BuyerVisualSearchProductCardResponse> Products,
    string Summary,
    string ImageRetentionNote);

public sealed record BuyerVisualSearchProductCardResponse(
    Guid ProductId,
    string Title,
    string Slug,
    string? SellerDisplayName,
    string? ImageUrl,
    decimal Price,
    string Currency,
    IReadOnlyCollection<string> MatchReasons,
    bool PersonalizationApplied,
    IReadOnlyCollection<string> PersonalizationReasons);
