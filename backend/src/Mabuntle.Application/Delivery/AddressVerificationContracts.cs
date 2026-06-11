using System.Text.Json;
using Mabuntle.Domain.Delivery;

namespace Mabuntle.Application.Delivery;

public interface IAddressVerificationService
{
    Task<AddressVerificationResult> VerifyAsync(
        AddressVerificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AddressVerificationRequest(
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null);

public sealed record AddressVerificationResult(
    AddressVerificationStatus Status,
    string Provider,
    IReadOnlyCollection<string> Warnings,
    DateTimeOffset VerifiedAtUtc,
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null);

public static class AddressVerificationWarningsJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyCollection<string>? warnings) =>
        JsonSerializer.Serialize(warnings ?? Array.Empty<string>(), SerializerOptions);

    public static IReadOnlyCollection<string> Deserialize(string? warningsJson)
    {
        if (string.IsNullOrWhiteSpace(warningsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(warningsJson, SerializerOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
