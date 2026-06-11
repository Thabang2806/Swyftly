using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Delivery;

public sealed class PickupPoint : AuditableEntity
{
    public const int ProviderNameMaxLength = 80;
    public const int CodeMaxLength = 80;
    public const int NameMaxLength = 160;
    public const int AddressLineMaxLength = 240;
    public const int SuburbMaxLength = 120;
    public const int CityMaxLength = 120;
    public const int ProvinceMaxLength = 120;
    public const int PostalCodeMaxLength = 32;
    public const int CountryCodeLength = 2;
    public const int OpeningHoursMaxLength = 500;

    private PickupPoint()
    {
    }

    public PickupPoint(
        string providerName,
        string code,
        string name,
        string addressLine1,
        string? addressLine2,
        string? suburb,
        string city,
        string province,
        string postalCode,
        string countryCode,
        decimal? latitude,
        decimal? longitude,
        string? openingHours,
        bool isActive)
    {
        Update(
            providerName,
            code,
            name,
            addressLine1,
            addressLine2,
            suburb,
            city,
            province,
            postalCode,
            countryCode,
            latitude,
            longitude,
            openingHours,
            isActive);
    }

    public string ProviderName { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string? Suburb { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Province { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public string? OpeningHours { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        string providerName,
        string code,
        string name,
        string addressLine1,
        string? addressLine2,
        string? suburb,
        string city,
        string province,
        string postalCode,
        string countryCode,
        decimal? latitude,
        decimal? longitude,
        string? openingHours,
        bool isActive)
    {
        ProviderName = Required(providerName, nameof(providerName), ProviderNameMaxLength);
        Code = Required(code, nameof(code), CodeMaxLength);
        Name = Required(name, nameof(name), NameMaxLength);
        AddressLine1 = Required(addressLine1, nameof(addressLine1), AddressLineMaxLength);
        AddressLine2 = Optional(addressLine2, nameof(addressLine2), AddressLineMaxLength);
        Suburb = Optional(suburb, nameof(suburb), SuburbMaxLength);
        City = Required(city, nameof(city), CityMaxLength);
        Province = Required(province, nameof(province), ProvinceMaxLength);
        PostalCode = Required(postalCode, nameof(postalCode), PostalCodeMaxLength);
        CountryCode = NormalizeCountryCode(countryCode);
        Latitude = ValidateLatitude(latitude);
        Longitude = ValidateLongitude(longitude);
        OpeningHours = Optional(openingHours, nameof(openingHours), OpeningHoursMaxLength);
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
        return string.Equals(CountryCode, NormalizeCountryCode(countryCode), StringComparison.OrdinalIgnoreCase)
            && string.Equals(Province, province.Trim(), StringComparison.OrdinalIgnoreCase);
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

    private static decimal? ValidateLatitude(decimal? value)
    {
        if (value is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(Latitude), "Latitude must be between -90 and 90.");
        }

        return value;
    }

    private static decimal? ValidateLongitude(decimal? value)
    {
        if (value is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(Longitude), "Longitude must be between -180 and 180.");
        }

        return value;
    }
}
