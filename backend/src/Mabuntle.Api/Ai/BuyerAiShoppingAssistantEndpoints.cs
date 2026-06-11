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

public static class BuyerAiShoppingAssistantEndpoints
{
    public static IEndpointRouteBuilder MapBuyerAiShoppingAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/buyer/ai/shopping-assistant", SearchAsync)
            .WithTags("Buyer AI")
            .WithName("SearchWithBuyerAiAssistant")
            .WithSummary("Extracts buyer shopping intent and returns real published product matches only.")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly)
            .RequireRateLimiting(MabuntleRateLimitPolicies.Ai)
            .Produces<BuyerAiShoppingAssistantResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        BuyerAiShoppingAssistantRequest request,
        ClaimsPrincipal principal,
        IAiShoppingIntentService intentService,
        ICurrentUser currentUser,
        IBuyerAiPersonalizationService personalizationService,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var intentResult = await intentService.ExtractIntentAsync(
            new ShoppingIntentExtractionRequest(request.Message),
            cancellationToken);
        if (intentResult.IsFailure)
        {
            return intentResult.ToHttpResult(HttpResults.Ok);
        }

        var intent = intentResult.Value;
        var candidates = await FindCandidatesAsync(intent, dbContext, cancellationToken);
        var cards = candidates
            .Select(candidate => new BuyerAiProductCardResponse(
                candidate.Product.Id,
                candidate.Product.Title ?? "Untitled product",
                candidate.Product.Slug ?? candidate.Product.Id.ToString("N"),
                candidate.SellerDisplayName,
                candidate.PrimaryImageUrl,
                candidate.MinPrice,
                "ZAR",
                BuildMatchReasons(intent, candidate.Product, candidate.CategoryName, candidate.VariantColours, candidate.VariantSizes),
                PersonalizationApplied: false,
                []))
            .ToArray();
        cards = await ApplyPersonalizationAsync(
            cards,
            ParseUserId(currentUser.UserId),
            personalizationService,
            timeProvider.GetUtcNow(),
            cancellationToken);

        await BuyerAiDiscoveryHistoryWriter.SaveAssistantHistoryIfEnabledAsync(
            principal,
            intent,
            cards,
            dbContext,
            timeProvider,
            cancellationToken);

        return HttpResults.Ok(new BuyerAiShoppingAssistantResponse(
            intent,
            cards,
            cards.Length == 0
                ? "No exact products matched. Try a broader category, colour, size, or budget."
                : "These matches come only from published Mabuntle products returned by the backend search.",
            intent.BeautySkinType is null && intent.BeautyConcern is null
                ? null
                : "Beauty matches are product-discovery results only and are not medical advice."));
    }

    private static async Task<IReadOnlyCollection<ProductCandidate>> FindCandidatesAsync(
        ShoppingIntent intent,
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
        var sellers = await dbContext.SellerProfiles
            .AsNoTracking()
            .Where(seller => sellerIds.Contains(seller.Id))
            .ToDictionaryAsync(seller => seller.Id, seller => seller.DisplayName, cancellationToken);
        var images = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId))
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
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
                    sellers.GetValueOrDefault(product.SellerId),
                    images.FirstOrDefault(image => image.ProductId == product.Id)?.Url,
                    productVariants.Min(variant => variant.Price),
                    productVariants.Select(variant => variant.Colour).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    productVariants.Select(variant => variant.Size).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            })
            .Where(candidate => MatchesIntent(intent, candidate))
            .OrderByDescending(candidate => Score(intent, candidate))
            .ThenBy(candidate => candidate.MinPrice)
            .Take(8)
            .ToArray();
    }

    private static bool MatchesIntent(ShoppingIntent intent, ProductCandidate candidate)
    {
        if (intent.BudgetMax.HasValue && candidate.MinPrice > intent.BudgetMax.Value)
        {
            return false;
        }

        var searchable = $"{candidate.Product.Title} {candidate.Product.ShortDescription} {candidate.Product.FullDescription} {candidate.CategoryName}".ToLowerInvariant();
        return ContainsIfPresent(searchable, intent.Category)
            && ContainsIfPresent(searchable, intent.Subcategory)
            && ContainsIfPresent(searchable, intent.Occasion)
            && ContainsIfPresent(searchable, intent.Style)
            && ContainsIfPresent(searchable, intent.Material)
            && (intent.Colour is null || candidate.VariantColours.Contains(intent.Colour, StringComparer.OrdinalIgnoreCase) || searchable.Contains(intent.Colour.ToLowerInvariant(), StringComparison.Ordinal))
            && (intent.Size is null || candidate.VariantSizes.Contains(intent.Size, StringComparer.OrdinalIgnoreCase));
    }

    private static int Score(ShoppingIntent intent, ProductCandidate candidate)
    {
        var score = 0;
        if (intent.Category is not null && string.Equals(candidate.CategoryName, intent.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (intent.Colour is not null && candidate.VariantColours.Contains(intent.Colour, StringComparer.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (intent.Size is not null && candidate.VariantSizes.Contains(intent.Size, StringComparer.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (intent.BudgetMax.HasValue && candidate.MinPrice <= intent.BudgetMax.Value)
        {
            score += 1;
        }

        return score;
    }

    private static bool ContainsIfPresent(string searchable, string? value) =>
        value is null || searchable.Contains(value.ToLowerInvariant(), StringComparison.Ordinal);

    private static Guid ParseUserId(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

    private static IReadOnlyCollection<string> BuildMatchReasons(
        ShoppingIntent intent,
        Product product,
        string? categoryName,
        IReadOnlyCollection<string> colours,
        IReadOnlyCollection<string> sizes)
    {
        var reasons = new List<string>();
        if (intent.Category is not null && string.Equals(categoryName, intent.Category, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"Matches {intent.Category}.");
        }

        if (intent.Colour is not null && colours.Contains(intent.Colour, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"Available in {intent.Colour}.");
        }

        if (intent.Size is not null && sizes.Contains(intent.Size, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"Available in size {intent.Size}.");
        }

        if (intent.BudgetMax.HasValue)
        {
            reasons.Add($"Within budget up to {intent.BudgetMax.Value:0.##}.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add($"Matched the search text for {product.Title ?? "this product"}.");
        }

        return reasons;
    }

    private static async Task<BuyerAiProductCardResponse[]> ApplyPersonalizationAsync(
        BuyerAiProductCardResponse[] cards,
        Guid userId,
        IBuyerAiPersonalizationService personalizationService,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (cards.Length == 0)
        {
            return cards;
        }

        var personalization = await personalizationService.PersonalizeAsync(
            userId,
            cards.Select(card => card.ProductId).ToArray(),
            now,
            cancellationToken);
        if (personalization.Count == 0)
        {
            return cards;
        }

        var byProductId = personalization.ToDictionary(item => item.ProductId);
        return cards
            .Select((card, index) =>
            {
                byProductId.TryGetValue(card.ProductId, out var result);
                return new
                {
                    Card = result is null
                        ? card
                        : card with
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
            .Select(item => item.Card)
            .ToArray();
    }

    private sealed record ProductCandidate(
        Product Product,
        string? CategoryName,
        string? SellerDisplayName,
        string? PrimaryImageUrl,
        decimal MinPrice,
        IReadOnlyCollection<string> VariantColours,
        IReadOnlyCollection<string> VariantSizes);
}

public sealed record BuyerAiShoppingAssistantRequest(string Message);

public sealed record BuyerAiShoppingAssistantResponse(
    ShoppingIntent Intent,
    IReadOnlyCollection<BuyerAiProductCardResponse> Products,
    string Summary,
    string? SafetyNote);

public sealed record BuyerAiProductCardResponse(
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
