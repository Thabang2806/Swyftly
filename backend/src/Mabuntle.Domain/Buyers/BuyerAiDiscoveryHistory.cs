using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerAiDiscoveryHistory : Entity
{
    public const int ContextFieldMaxLength = 100;
    public const int SourceRouteMaxLength = 300;
    public const int MaxProductIds = 20;

    private BuyerAiDiscoveryHistory()
    {
        ProductIds = [];
    }

    public BuyerAiDiscoveryHistory(
        Guid buyerId,
        BuyerGrowthSourceTool sourceTool,
        DateTimeOffset createdAtUtc,
        string? category,
        string? colour,
        string? material,
        BuyerGrowthConfidenceBand? confidenceBand,
        int resultCount,
        IEnumerable<Guid> productIds,
        string? sourceRoute)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (resultCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultCount), "Result count cannot be negative.");
        }

        BuyerId = buyerId;
        SourceTool = sourceTool;
        Category = TrimOrNull(category, ContextFieldMaxLength);
        Colour = TrimOrNull(colour, ContextFieldMaxLength);
        Material = TrimOrNull(material, ContextFieldMaxLength);
        ConfidenceBand = confidenceBand;
        ResultCount = resultCount;
        ProductIds = productIds
            .Where(productId => productId != Guid.Empty)
            .Distinct()
            .Take(MaxProductIds)
            .ToArray();
        SourceRoute = TrimOrNull(sourceRoute, SourceRouteMaxLength);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BuyerId { get; private set; }

    public BuyerGrowthSourceTool SourceTool { get; private set; }

    public string? Category { get; private set; }

    public string? Colour { get; private set; }

    public string? Material { get; private set; }

    public BuyerGrowthConfidenceBand? ConfidenceBand { get; private set; }

    public int ResultCount { get; private set; }

    public Guid[] ProductIds { get; private set; }

    public string? SourceRoute { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
