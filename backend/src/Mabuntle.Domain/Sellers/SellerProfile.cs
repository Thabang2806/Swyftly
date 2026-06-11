using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerProfile : AuditableEntity
{
    private SellerProfile()
    {
    }

    public SellerProfile(Guid userId)
    {
        UserId = userId;
        VerificationStatus = SellerVerificationStatus.PendingVerification;
    }

    public Guid UserId { get; private set; }

    public string? DisplayName { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? PhoneNumber { get; private set; }

    public SellerBusinessType? BusinessType { get; private set; }

    public string? BusinessName { get; private set; }

    public SellerVerificationStatus VerificationStatus { get; private set; }

    public void UpdateProfile(
        string displayName,
        string contactEmail,
        string phoneNumber,
        SellerBusinessType businessType,
        string? businessName)
    {
        DisplayName = Required(displayName, nameof(displayName));
        ContactEmail = Required(contactEmail, nameof(contactEmail));
        PhoneNumber = Required(phoneNumber, nameof(phoneNumber));
        BusinessType = businessType;
        BusinessName = businessType == SellerBusinessType.RegisteredBusiness
            ? Required(businessName, nameof(businessName))
            : TrimOrNull(businessName);
    }

    public bool HasRequiredProfileFields()
    {
        return HasValue(DisplayName)
            && HasValue(ContactEmail)
            && HasValue(PhoneNumber)
            && BusinessType.HasValue
            && (BusinessType != SellerBusinessType.RegisteredBusiness || HasValue(BusinessName));
    }

    public bool CanSubmitForVerification(
        SellerStorefront? storefront,
        SellerAddress? address,
        SellerPayoutProfilePlaceholder? payoutProfile)
    {
        return HasRequiredProfileFields()
            && storefront?.HasRequiredFields() == true
            && address?.HasRequiredFields() == true
            && payoutProfile?.HasSubmittedPlaceholder == true;
    }

    public bool CanBeVerified(
        SellerStorefront? storefront,
        SellerAddress? address,
        SellerPayoutProfilePlaceholder? payoutProfile)
    {
        return CanSubmitForVerification(storefront, address, payoutProfile)
            && payoutProfile?.IsAdminApproved == true;
    }

    public void SubmitForVerification(
        SellerStorefront? storefront,
        SellerAddress? address,
        SellerPayoutProfilePlaceholder? payoutProfile)
    {
        if (!CanSubmitForVerification(storefront, address, payoutProfile))
        {
            throw new InvalidOperationException("Seller onboarding fields must be complete before verification review.");
        }

        VerificationStatus = SellerVerificationStatus.UnderReview;
    }

    public void MarkVerified(
        SellerStorefront? storefront,
        SellerAddress? address,
        SellerPayoutProfilePlaceholder? payoutProfile)
    {
        if (!CanBeVerified(storefront, address, payoutProfile))
        {
            throw new InvalidOperationException("Seller cannot be verified until onboarding fields are complete and payout details are admin approved.");
        }

        VerificationStatus = SellerVerificationStatus.Verified;
    }

    public void MarkRejected(string reason)
    {
        _ = Required(reason, nameof(reason));
        VerificationStatus = SellerVerificationStatus.Rejected;
    }

    public void Suspend()
    {
        VerificationStatus = SellerVerificationStatus.Suspended;
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
