using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerStorePolicy : AuditableEntity
{
    public const int ReturnPolicyMaxLength = 2000;
    public const int ExchangePolicyMaxLength = 2000;
    public const int FulfilmentPolicyMaxLength = 1000;
    public const int SupportPolicyMaxLength = 1000;
    public const int CareInstructionsMaxLength = 1000;
    public const int ProductDisclaimerMaxLength = 1000;
    public const int MaxReturnWindowDays = 365;

    private SellerStorePolicy()
    {
    }

    public SellerStorePolicy(
        Guid sellerId,
        int? returnWindowDays,
        string? returnPolicy,
        string? exchangePolicy,
        string? fulfilmentPolicy,
        string? supportPolicy,
        string? careInstructions,
        string? productDisclaimer)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        Update(
            returnWindowDays,
            returnPolicy,
            exchangePolicy,
            fulfilmentPolicy,
            supportPolicy,
            careInstructions,
            productDisclaimer);
    }

    public Guid SellerId { get; private set; }

    public int? ReturnWindowDays { get; private set; }

    public string? ReturnPolicy { get; private set; }

    public string? ExchangePolicy { get; private set; }

    public string? FulfilmentPolicy { get; private set; }

    public string? SupportPolicy { get; private set; }

    public string? CareInstructions { get; private set; }

    public string? ProductDisclaimer { get; private set; }

    public void Update(
        int? returnWindowDays,
        string? returnPolicy,
        string? exchangePolicy,
        string? fulfilmentPolicy,
        string? supportPolicy,
        string? careInstructions,
        string? productDisclaimer)
    {
        if (returnWindowDays is < 0 or > MaxReturnWindowDays)
        {
            throw new ArgumentOutOfRangeException(nameof(returnWindowDays), $"Return window must be between 0 and {MaxReturnWindowDays} days.");
        }

        ReturnWindowDays = returnWindowDays;
        ReturnPolicy = Optional(returnPolicy, nameof(returnPolicy), ReturnPolicyMaxLength);
        ExchangePolicy = Optional(exchangePolicy, nameof(exchangePolicy), ExchangePolicyMaxLength);
        FulfilmentPolicy = Optional(fulfilmentPolicy, nameof(fulfilmentPolicy), FulfilmentPolicyMaxLength);
        SupportPolicy = Optional(supportPolicy, nameof(supportPolicy), SupportPolicyMaxLength);
        CareInstructions = Optional(careInstructions, nameof(careInstructions), CareInstructionsMaxLength);
        ProductDisclaimer = Optional(productDisclaimer, nameof(productDisclaimer), ProductDisclaimerMaxLength);
    }

    private static string? Optional(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must be {maxLength} characters or fewer.", parameterName);
        }

        return normalized;
    }
}
