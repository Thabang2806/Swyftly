using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerGrowthOutcome : Entity
{
    private BuyerGrowthOutcome()
    {
    }

    public BuyerGrowthOutcome(
        Guid buyerId,
        BuyerGrowthOutcomeType outcomeType,
        BuyerGrowthSourceTool sourceTool,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset attributedFromUtc,
        int attributionWindowMinutes,
        Guid? sourceEventId = null,
        Guid? productId = null,
        Guid? cartId = null,
        Guid? orderId = null,
        BuyerGrowthConfidenceBand? confidenceBand = null)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (attributionWindowMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attributionWindowMinutes), "Attribution window must be positive.");
        }

        BuyerId = buyerId;
        OutcomeType = outcomeType;
        SourceTool = sourceTool;
        SourceEventId = sourceEventId == Guid.Empty ? null : sourceEventId;
        ProductId = productId == Guid.Empty ? null : productId;
        CartId = cartId == Guid.Empty ? null : cartId;
        OrderId = orderId == Guid.Empty ? null : orderId;
        ConfidenceBand = confidenceBand;
        OccurredAtUtc = occurredAtUtc;
        AttributedFromUtc = attributedFromUtc;
        AttributionWindowMinutes = attributionWindowMinutes;
    }

    public Guid BuyerId { get; private set; }

    public BuyerGrowthOutcomeType OutcomeType { get; private set; }

    public BuyerGrowthSourceTool SourceTool { get; private set; }

    public Guid? SourceEventId { get; private set; }

    public Guid? ProductId { get; private set; }

    public Guid? CartId { get; private set; }

    public Guid? OrderId { get; private set; }

    public BuyerGrowthConfidenceBand? ConfidenceBand { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset AttributedFromUtc { get; private set; }

    public int AttributionWindowMinutes { get; private set; }
}

public enum BuyerGrowthOutcomeType
{
    ProductOpened = 0,
    ProductAddedToCart = 1,
    CheckoutStarted = 2,
    OrderCreated = 3,
    OrderPaid = 4
}
