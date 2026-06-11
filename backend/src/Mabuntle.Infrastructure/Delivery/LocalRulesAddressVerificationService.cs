using Mabuntle.Application.Delivery;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Delivery;

namespace Mabuntle.Infrastructure.Delivery;

public sealed class LocalRulesAddressVerificationService(TimeProvider timeProvider) : IAddressVerificationService
{
    public const string ProviderName = "LocalRules";

    private static readonly Dictionary<string, string> SouthAfricanProvinceAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Eastern Cape"] = "Eastern Cape",
        ["EC"] = "Eastern Cape",
        ["Free State"] = "Free State",
        ["FS"] = "Free State",
        ["Gauteng"] = "Gauteng",
        ["GP"] = "Gauteng",
        ["KwaZulu-Natal"] = "KwaZulu-Natal",
        ["KwaZulu Natal"] = "KwaZulu-Natal",
        ["Kwazulu-Natal"] = "KwaZulu-Natal",
        ["Kwazulu Natal"] = "KwaZulu-Natal",
        ["KZN"] = "KwaZulu-Natal",
        ["Limpopo"] = "Limpopo",
        ["LP"] = "Limpopo",
        ["Mpumalanga"] = "Mpumalanga",
        ["MP"] = "Mpumalanga",
        ["North West"] = "North West",
        ["NW"] = "North West",
        ["Northern Cape"] = "Northern Cape",
        ["NC"] = "Northern Cape",
        ["Western Cape"] = "Western Cape",
        ["WC"] = "Western Cape"
    };

    public Task<AddressVerificationResult> VerifyAsync(
        AddressVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = new BuyerDeliveryAddress(
            Guid.NewGuid(),
            "Verification",
            request.RecipientName,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.Suburb,
            request.City,
            NormalizeProvince(request.CountryCode, request.Province, out var provinceWarning),
            request.PostalCode,
            request.CountryCode,
            isDefault: false,
            request.DeliveryInstructions);

        var warnings = new List<string>();
        if (provinceWarning is not null)
        {
            warnings.Add(provinceWarning);
        }

        if (!string.Equals(normalized.CountryCode, "ZA", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Address verification currently uses local South African rules only.");
        }
        else if (!IsValidSouthAfricanPostalCode(normalized.PostalCode))
        {
            warnings.Add("South African postal codes should be 4 digits.");
        }

        var status = warnings.Count == 0
            ? AddressVerificationStatus.Verified
            : AddressVerificationStatus.NeedsReview;

        return Task.FromResult(new AddressVerificationResult(
            status,
            ProviderName,
            warnings,
            timeProvider.GetUtcNow(),
            normalized.RecipientName,
            normalized.PhoneNumber,
            normalized.AddressLine1,
            normalized.AddressLine2,
            normalized.Suburb,
            normalized.City,
            normalized.Province,
            normalized.PostalCode,
            normalized.CountryCode,
            normalized.DeliveryInstructions));
    }

    private static string NormalizeProvince(string countryCode, string province, out string? warning)
    {
        warning = null;
        var normalizedCountry = countryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedProvince = province?.Trim() ?? string.Empty;

        if (normalizedCountry != "ZA")
        {
            return normalizedProvince;
        }

        if (SouthAfricanProvinceAliases.TryGetValue(normalizedProvince, out var canonical))
        {
            return canonical;
        }

        warning = "Province was not recognised against the local South African province list.";
        return normalizedProvince;
    }

    private static bool IsValidSouthAfricanPostalCode(string postalCode) =>
        postalCode.Length == 4 && postalCode.All(char.IsDigit);
}
