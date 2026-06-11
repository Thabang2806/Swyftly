using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class CommissionRule : Entity
{
    private CommissionRule()
    {
    }

    public CommissionRule(
        string name,
        decimal platformCommissionRatePercent,
        decimal paymentProviderFeeRatePercent,
        decimal paymentProviderFixedFee,
        string currency,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        if (platformCommissionRatePercent < 0 || paymentProviderFeeRatePercent < 0 || paymentProviderFixedFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(platformCommissionRatePercent), "Commission and fee values cannot be negative.");
        }

        Name = Required(name, nameof(name));
        PlatformCommissionRatePercent = platformCommissionRatePercent;
        PaymentProviderFeeRatePercent = paymentProviderFeeRatePercent;
        PaymentProviderFixedFee = paymentProviderFixedFee;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public decimal PlatformCommissionRatePercent { get; private set; }

    public decimal PaymentProviderFeeRatePercent { get; private set; }

    public decimal PaymentProviderFixedFee { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

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
