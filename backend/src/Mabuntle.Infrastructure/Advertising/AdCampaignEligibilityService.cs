using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Advertising;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Advertising;

public sealed class AdCampaignEligibilityService(MabuntleDbContext dbContext) : IAdCampaignEligibilityService
{
    public const int MinimumProductQualityScore = 80;

    public async Task<AdCampaignEligibilityResult> ValidateAsync(
        Guid sellerId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var sellerReasons = new List<string>();
        var seller = await dbContext.SellerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sellerId, cancellationToken);

        if (seller is null)
        {
            sellerReasons.Add("Seller was not found.");
        }
        else
        {
            if (seller.VerificationStatus != SellerVerificationStatus.Verified)
            {
                sellerReasons.Add("Seller must be verified before creating ad campaigns.");
            }

            if (seller.VerificationStatus == SellerVerificationStatus.Suspended)
            {
                sellerReasons.Add("Suspended sellers cannot create ad campaigns.");
            }
        }

        var hasSeriousDispute = await dbContext.Disputes
            .AsNoTracking()
            .AnyAsync(
                dispute => dispute.SellerId == sellerId && dispute.Status == DisputeStatus.UnderAdminReview,
                cancellationToken);
        if (hasSeriousDispute)
        {
            sellerReasons.Add("Seller has an active serious dispute under admin review.");
        }

        var distinctProductIds = productIds
            .Where(productId => productId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctProductIds.Length == 0)
        {
            sellerReasons.Add("At least one product is required.");
        }

        var productResults = new List<AdProductEligibilityResult>();
        foreach (var productId in distinctProductIds)
        {
            productResults.Add(await ValidateProductAsync(sellerId, productId, cancellationToken));
        }

        return new AdCampaignEligibilityResult(
            sellerId,
            sellerReasons.Count == 0 && productResults.All(product => product.IsEligible),
            productResults,
            sellerReasons);
    }

    private async Task<AdProductEligibilityResult> ValidateProductAsync(
        Guid sellerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);

        if (product is null)
        {
            return new AdProductEligibilityResult(productId, false, 0, ["Product was not found."]);
        }

        if (product.SellerId != sellerId)
        {
            reasons.Add("Product does not belong to the authenticated seller.");
        }

        if (product.Status != ProductStatus.Published)
        {
            reasons.Add("Product must be published.");
        }

        var hasSellableStock = await dbContext.ProductVariants
            .AsNoTracking()
            .AnyAsync(
                variant => variant.ProductId == product.Id
                    && variant.Status == ProductVariantStatus.Active
                    && variant.StockQuantity > variant.ReservedQuantity,
                cancellationToken);
        if (!hasSellableStock)
        {
            reasons.Add("Product must have at least one active in-stock variant.");
        }

        var latestModeration = await dbContext.AiModerationResults
            .AsNoTracking()
            .Where(result => result.ProductId == product.Id)
            .OrderByDescending(result => result.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestModeration is not null &&
            (latestModeration.NeedsAdminReview ||
             latestModeration.RiskLevel != AiModerationRiskLevel.Low ||
             HasJsonArrayEntries(latestModeration.FlagsJson)))
        {
            reasons.Add("Product must not have unresolved moderation flags.");
        }

        var qualityScore = await CalculateQualityScoreAsync(product, hasSellableStock, cancellationToken);
        if (qualityScore < MinimumProductQualityScore)
        {
            reasons.Add($"Product quality score must be at least {MinimumProductQualityScore}.");
        }

        return new AdProductEligibilityResult(productId, reasons.Count == 0, qualityScore, reasons);
    }

    private async Task<int> CalculateQualityScoreAsync(
        Product product,
        bool hasSellableStock,
        CancellationToken cancellationToken)
    {
        var hasImage = await dbContext.ProductImages
            .AsNoTracking()
            .AnyAsync(image => image.ProductId == product.Id, cancellationToken);
        var hasAttributes = await dbContext.ProductAttributeValues
            .AsNoTracking()
            .AnyAsync(attribute => attribute.ProductId == product.Id, cancellationToken);

        var score = 0;
        score += product.CategoryId.HasValue ? 10 : 0;
        score += HasValue(product.Title) ? 15 : 0;
        score += HasValue(product.ShortDescription) ? 15 : 0;
        score += HasValue(product.FullDescription) ? 20 : 0;
        score += hasAttributes ? 10 : 0;
        score += hasImage ? 15 : 0;
        score += hasSellableStock ? 15 : 0;

        return score;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool HasJsonArrayEntries(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                document.RootElement.GetArrayLength() > 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return true;
        }
    }
}
