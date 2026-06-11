using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerGrowthEvent : Entity
{
    public const int ContextFieldMaxLength = 100;
    public const int SourceRouteMaxLength = 300;

    private BuyerGrowthEvent()
    {
    }

    public BuyerGrowthEvent(
        Guid buyerId,
        BuyerGrowthEventType eventType,
        BuyerGrowthSourceTool sourceTool,
        DateTimeOffset occurredAtUtc,
        Guid? productId = null,
        int? resultCount = null,
        BuyerGrowthConfidenceBand? confidenceBand = null,
        string? category = null,
        string? colour = null,
        string? material = null,
        string? sourceRoute = null,
        BuyerGrowthFeedbackReason? feedbackReason = null)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        BuyerId = buyerId;
        EventType = eventType;
        SourceTool = sourceTool;
        ProductId = productId == Guid.Empty ? null : productId;
        ResultCount = resultCount;
        ConfidenceBand = confidenceBand;
        Category = TrimOrNull(category, ContextFieldMaxLength);
        Colour = TrimOrNull(colour, ContextFieldMaxLength);
        Material = TrimOrNull(material, ContextFieldMaxLength);
        SourceRoute = TrimOrNull(sourceRoute, SourceRouteMaxLength);
        FeedbackReason = feedbackReason;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid BuyerId { get; private set; }

    public BuyerGrowthEventType EventType { get; private set; }

    public BuyerGrowthSourceTool SourceTool { get; private set; }

    public Guid? ProductId { get; private set; }

    public int? ResultCount { get; private set; }

    public BuyerGrowthConfidenceBand? ConfidenceBand { get; private set; }

    public string? Category { get; private set; }

    public string? Colour { get; private set; }

    public string? Material { get; private set; }

    public string? SourceRoute { get; private set; }

    public BuyerGrowthFeedbackReason? FeedbackReason { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

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
