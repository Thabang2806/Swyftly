using Mabuntle.Domain.Delivery;

namespace Mabuntle.Domain.Orders;

public sealed record OrderDeliveryAddress(
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null,
    AddressVerificationStatus VerificationStatus = AddressVerificationStatus.Unverified,
    string? VerificationProvider = null,
    string? VerificationWarningsJson = null,
    DateTimeOffset? VerifiedAtUtc = null);
