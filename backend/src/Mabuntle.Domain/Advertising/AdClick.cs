using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdClick : Entity
{
    private AdClick()
    {
    }

    public AdClick(Guid adCampaignId, Guid productId, Guid? buyerId, string? anonymousVisitorId, DateTimeOffset occurredAtUtc)
    {
        if (adCampaignId == Guid.Empty)
        {
            throw new ArgumentException("Ad campaign id is required.", nameof(adCampaignId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        AdCampaignId = adCampaignId;
        ProductId = productId;
        BuyerId = buyerId == Guid.Empty ? null : buyerId;
        AnonymousVisitorId = TrimOrNull(anonymousVisitorId, 128);
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? BuyerId { get; private set; }

    public string? AnonymousVisitorId { get; private set; }

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
