using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdConversion : Entity
{
    private AdConversion()
    {
    }

    public AdConversion(Guid adCampaignId, Guid adClickId, Guid orderId, decimal revenueAmount, string currency, DateTimeOffset occurredAtUtc)
    {
        if (adCampaignId == Guid.Empty)
        {
            throw new ArgumentException("Ad campaign id is required.", nameof(adCampaignId));
        }

        if (adClickId == Guid.Empty)
        {
            throw new ArgumentException("Ad click id is required.", nameof(adClickId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (revenueAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revenueAmount), "Revenue amount must be positive.");
        }

        AdCampaignId = adCampaignId;
        AdClickId = adClickId;
        OrderId = orderId;
        RevenueAmount = revenueAmount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public Guid AdClickId { get; private set; }

    public Guid OrderId { get; private set; }

    public decimal RevenueAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }
}
