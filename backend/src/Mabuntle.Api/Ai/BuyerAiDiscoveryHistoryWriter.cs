using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Ai;
using Mabuntle.Domain.Buyers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Api.Ai;

internal static class BuyerAiDiscoveryHistoryWriter
{
    public static async Task SaveAssistantHistoryIfEnabledAsync(
        ClaimsPrincipal principal,
        ShoppingIntent intent,
        IReadOnlyCollection<BuyerAiProductCardResponse> products,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetOptedInBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return;
        }

        dbContext.BuyerAiDiscoveryHistory.Add(new BuyerAiDiscoveryHistory(
            buyer.Id,
            BuyerGrowthSourceTool.Assistant,
            timeProvider.GetUtcNow(),
            intent.Category,
            intent.Colour,
            intent.Material,
            ResolveAssistantConfidence(intent, products),
            products.Count,
            products.Select(product => product.ProductId),
            "/assistant"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task SaveVisualSearchHistoryIfEnabledAsync(
        ClaimsPrincipal principal,
        VisualSearchAttributes attributes,
        IReadOnlyCollection<BuyerVisualSearchProductCardResponse> products,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetOptedInBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return;
        }

        dbContext.BuyerAiDiscoveryHistory.Add(new BuyerAiDiscoveryHistory(
            buyer.Id,
            BuyerGrowthSourceTool.VisualSearch,
            timeProvider.GetUtcNow(),
            attributes.Category,
            attributes.Colour,
            attributes.MaterialGuess,
            ResolveVisualConfidence(attributes),
            products.Count,
            products.Select(product => product.ProductId),
            "/visual-search"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<BuyerProfile?> GetOptedInBuyerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        var buyer = await dbContext.BuyerProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (buyer is null)
        {
            return null;
        }

        var historyEnabled = await dbContext.BuyerAiDiscoveryPreferences
            .AsNoTracking()
            .AnyAsync(preference => preference.BuyerId == buyer.Id && preference.HistoryEnabled, cancellationToken);

        return historyEnabled ? buyer : null;
    }

    private static BuyerGrowthConfidenceBand ResolveAssistantConfidence(
        ShoppingIntent intent,
        IReadOnlyCollection<BuyerAiProductCardResponse> products)
    {
        var extractedFieldCount = new[]
        {
            intent.Category,
            intent.Subcategory,
            intent.Size,
            intent.Colour,
            intent.Occasion,
            intent.Style,
            intent.Material,
            intent.Brand,
            intent.BeautySkinType,
            intent.BeautyConcern
        }.Count(value => !string.IsNullOrWhiteSpace(value));

        var score = 0;
        if (!intent.IsVague)
        {
            score++;
        }

        if (extractedFieldCount >= 3)
        {
            score++;
        }

        if (products.Count > 0)
        {
            score++;
        }

        if (products.Count >= 3)
        {
            score++;
        }

        if (string.IsNullOrWhiteSpace(intent.ClarificationPrompt))
        {
            score++;
        }

        return score >= 4
            ? BuyerGrowthConfidenceBand.High
            : score >= 2
                ? BuyerGrowthConfidenceBand.Medium
                : BuyerGrowthConfidenceBand.Low;
    }

    private static BuyerGrowthConfidenceBand ResolveVisualConfidence(VisualSearchAttributes attributes) =>
        attributes.Confidence >= 0.75m
            ? BuyerGrowthConfidenceBand.High
            : attributes.Confidence >= 0.45m
                ? BuyerGrowthConfidenceBand.Medium
                : BuyerGrowthConfidenceBand.Low;
}
