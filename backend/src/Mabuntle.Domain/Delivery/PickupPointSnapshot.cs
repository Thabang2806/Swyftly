namespace Mabuntle.Domain.Delivery;

public sealed record PickupPointSnapshot(
    Guid PickupPointId,
    string ProviderName,
    string Code,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    string? OpeningHours);
