using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdCharge : Entity
{
    private AdCharge()
    {
    }

    public AdCharge(Guid adCampaignId, Guid? adClickId, decimal amount, string currency, string reason, DateTimeOffset chargedAtUtc)
    {
        if (adCampaignId == Guid.Empty)
        {
            throw new ArgumentException("Ad campaign id is required.", nameof(adCampaignId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Charge amount must be positive.");
        }

        AdCampaignId = adCampaignId;
        AdClickId = adClickId == Guid.Empty ? null : adClickId;
        Amount = amount;
        Currency = Required(currency, nameof(currency), 3).ToUpperInvariant();
        Reason = Required(reason, nameof(reason), 240);
        ChargedAtUtc = chargedAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public Guid? AdClickId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset ChargedAtUtc { get; private set; }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
