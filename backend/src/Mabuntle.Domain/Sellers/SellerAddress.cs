using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerAddress : AuditableEntity
{
    private SellerAddress()
    {
    }

    public SellerAddress(
        Guid sellerId,
        string addressLine1,
        string? addressLine2,
        string city,
        string province,
        string postalCode,
        string countryCode)
    {
        SellerId = sellerId;
        AddressLine1 = Required(addressLine1, nameof(addressLine1));
        AddressLine2 = TrimOrNull(addressLine2);
        City = Required(city, nameof(city));
        Province = Required(province, nameof(province));
        PostalCode = Required(postalCode, nameof(postalCode));
        CountryCode = Required(countryCode, nameof(countryCode)).ToUpperInvariant();
    }

    public Guid SellerId { get; private set; }

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Province { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public bool HasRequiredFields()
    {
        return HasValue(AddressLine1)
            && HasValue(City)
            && HasValue(Province)
            && HasValue(PostalCode)
            && HasValue(CountryCode);
    }

    public void Update(
        string addressLine1,
        string? addressLine2,
        string city,
        string province,
        string postalCode,
        string countryCode)
    {
        AddressLine1 = Required(addressLine1, nameof(addressLine1));
        AddressLine2 = TrimOrNull(addressLine2);
        City = Required(city, nameof(city));
        Province = Required(province, nameof(province));
        PostalCode = Required(postalCode, nameof(postalCode));
        CountryCode = Required(countryCode, nameof(countryCode)).ToUpperInvariant();
    }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
