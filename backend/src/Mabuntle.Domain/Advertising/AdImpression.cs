using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdImpression : Entity
{
    private AdImpression()
    {
    }

    public AdImpression(Guid adCampaignId, Guid productId, string placement, string? anonymousVisitorId, DateTimeOffset occurredAtUtc)
    {
        AdCampaignId = RequiredId(adCampaignId, nameof(adCampaignId));
        ProductId = RequiredId(productId, nameof(productId));
        Placement = Required(placement, nameof(placement), 120);
        AnonymousVisitorId = TrimOrNull(anonymousVisitorId, 128);
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Placement { get; private set; } = string.Empty;

    public string? AnonymousVisitorId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private static Guid RequiredId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("Id is required.", parameterName) : value;

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

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
