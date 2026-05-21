using Swyftly.Domain.Common;

namespace Swyftly.Domain.Sellers;

public sealed class SellerDeliveryMethod : AuditableEntity
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int CountryCodeLength = 2;
    public const int ProvinceMaxLength = 120;

    private SellerDeliveryMethod()
    {
    }

    public SellerDeliveryMethod(
        Guid sellerId,
        string name,
        string? description,
        SellerDeliveryMethodType methodType,
        string countryCode,
        string? province,
        decimal basePrice,
        decimal? freeShippingThreshold,
        int estimatedMinDays,
        int estimatedMaxDays,
        int displayOrder,
        bool isActive)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        Update(
            name,
            description,
            methodType,
            countryCode,
            province,
            basePrice,
            freeShippingThreshold,
            estimatedMinDays,
            estimatedMaxDays,
            displayOrder,
            isActive);
    }

    public Guid SellerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public SellerDeliveryMethodType MethodType { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public string? Province { get; private set; }

    public decimal BasePrice { get; private set; }

    public decimal? FreeShippingThreshold { get; private set; }

    public int EstimatedMinDays { get; private set; }

    public int EstimatedMaxDays { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        string name,
        string? description,
        SellerDeliveryMethodType methodType,
        string countryCode,
        string? province,
        decimal basePrice,
        decimal? freeShippingThreshold,
        int estimatedMinDays,
        int estimatedMaxDays,
        int displayOrder,
        bool isActive)
    {
        ValidateMoney(basePrice, nameof(basePrice), allowZero: true);
        if (freeShippingThreshold.HasValue)
        {
            ValidateMoney(freeShippingThreshold.Value, nameof(freeShippingThreshold), allowZero: false);
        }

        if (estimatedMinDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedMinDays), "Estimated minimum days cannot be negative.");
        }

        if (estimatedMaxDays < estimatedMinDays)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedMaxDays), "Estimated maximum days must be greater than or equal to minimum days.");
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");
        }

        Name = Required(name, nameof(name), NameMaxLength);
        Description = Optional(description, nameof(description), DescriptionMaxLength);
        MethodType = methodType;
        CountryCode = NormalizeCountryCode(countryCode);
        Province = Optional(province, nameof(province), ProvinceMaxLength);
        BasePrice = basePrice;
        FreeShippingThreshold = freeShippingThreshold;
        EstimatedMinDays = estimatedMinDays;
        EstimatedMaxDays = estimatedMaxDays;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool MatchesAddress(string countryCode, string province)
    {
        var normalizedCountryCode = NormalizeCountryCode(countryCode);
        if (!string.Equals(CountryCode, normalizedCountryCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Province is null
            || string.Equals(Province, province.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public decimal CalculateShippingAmount(decimal cartSubtotal)
    {
        if (cartSubtotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cartSubtotal), "Cart subtotal cannot be negative.");
        }

        return FreeShippingThreshold.HasValue && cartSubtotal >= FreeShippingThreshold.Value
            ? 0
            : BasePrice;
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var normalized = Optional(value, parameterName, maxLength);
        if (normalized is null)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return normalized;
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

    private static string NormalizeCountryCode(string? value)
    {
        var normalized = Required(value, nameof(CountryCode), CountryCodeLength).ToUpperInvariant();
        if (normalized.Length != CountryCodeLength || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Country code must be a two-letter ISO code.", nameof(CountryCode));
        }

        return normalized;
    }

    private static void ValidateMoney(decimal amount, string parameterName, bool allowZero)
    {
        if (amount < 0 || (!allowZero && amount == 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, allowZero ? "Amount cannot be negative." : "Amount must be greater than zero.");
        }
    }
}
