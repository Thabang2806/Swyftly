using Swyftly.Domain.Common;

namespace Swyftly.Domain.Buyers;

public sealed class BuyerDeliveryAddress : AuditableEntity
{
    public const int MaxAddressesPerBuyer = 10;
    public const int LabelMaxLength = 80;
    public const int RecipientNameMaxLength = 160;
    public const int PhoneNumberMaxLength = 64;
    public const int AddressLineMaxLength = 240;
    public const int SuburbMaxLength = 120;
    public const int CityMaxLength = 120;
    public const int ProvinceMaxLength = 120;
    public const int PostalCodeMaxLength = 32;
    public const int CountryCodeLength = 2;
    public const int DeliveryInstructionsMaxLength = 500;

    private BuyerDeliveryAddress()
    {
    }

    public BuyerDeliveryAddress(
        Guid buyerId,
        string label,
        string recipientName,
        string phoneNumber,
        string addressLine1,
        string? addressLine2,
        string? suburb,
        string city,
        string province,
        string postalCode,
        string countryCode,
        bool isDefault,
        string? deliveryInstructions = null)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        BuyerId = buyerId;
        Update(label, recipientName, phoneNumber, addressLine1, addressLine2, suburb, city, province, postalCode, countryCode, isDefault, deliveryInstructions);
    }

    public Guid BuyerId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string? Suburb { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Province { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? DeliveryInstructions { get; private set; }

    public bool IsDefault { get; private set; }

    public void Update(
        string label,
        string recipientName,
        string phoneNumber,
        string addressLine1,
        string? addressLine2,
        string? suburb,
        string city,
        string province,
        string postalCode,
        string countryCode,
        bool isDefault,
        string? deliveryInstructions = null)
    {
        Label = Required(label, nameof(label), LabelMaxLength);
        RecipientName = Required(recipientName, nameof(recipientName), RecipientNameMaxLength);
        PhoneNumber = Required(phoneNumber, nameof(phoneNumber), PhoneNumberMaxLength);
        AddressLine1 = Required(addressLine1, nameof(addressLine1), AddressLineMaxLength);
        AddressLine2 = Optional(addressLine2, nameof(addressLine2), AddressLineMaxLength);
        Suburb = Optional(suburb, nameof(suburb), SuburbMaxLength);
        City = Required(city, nameof(city), CityMaxLength);
        Province = Required(province, nameof(province), ProvinceMaxLength);
        PostalCode = Required(postalCode, nameof(postalCode), PostalCodeMaxLength);
        CountryCode = NormalizeCountryCode(countryCode);
        DeliveryInstructions = Optional(deliveryInstructions, nameof(deliveryInstructions), DeliveryInstructionsMaxLength);
        IsDefault = isDefault;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
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
}
