using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerProfile : AuditableEntity
{
    public const int DisplayNameMaxLength = 160;
    public const int PhoneNumberMaxLength = 64;

    private BuyerProfile()
    {
    }

    public BuyerProfile(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public string? DisplayName { get; private set; }

    public string? PhoneNumber { get; private set; }

    public void UpdateSettings(string? displayName, string? phoneNumber)
    {
        DisplayName = Optional(displayName, DisplayNameMaxLength, nameof(displayName));
        PhoneNumber = Optional(phoneNumber, PhoneNumberMaxLength, nameof(phoneNumber));
    }

    private static string? Optional(string? value, int maxLength, string paramName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return normalized;
    }
}
