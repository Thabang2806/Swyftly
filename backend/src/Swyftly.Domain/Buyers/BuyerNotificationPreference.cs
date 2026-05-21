using Swyftly.Domain.Common;

namespace Swyftly.Domain.Buyers;

public sealed class BuyerNotificationPreference : AuditableEntity
{
    private BuyerNotificationPreference()
    {
        Category = string.Empty;
    }

    public BuyerNotificationPreference(Guid buyerId, string category, bool isEnabled, bool emailEnabled = true)
    {
        BuyerId = buyerId;
        Category = RequiredCategory(category);
        IsEnabled = isEnabled;
        EmailEnabled = emailEnabled;
    }

    public Guid BuyerId { get; private set; }

    public string Category { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public bool EmailEnabled { get; private set; } = true;

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void SetChannels(bool isEnabled, bool emailEnabled)
    {
        IsEnabled = isEnabled;
        EmailEnabled = emailEnabled;
    }

    private static string RequiredCategory(string category)
    {
        var normalized = category.Trim();
        if (!BuyerNotificationCategory.IsSupported(normalized))
        {
            throw new ArgumentException($"Notification category '{category}' is not supported.", nameof(category));
        }

        return normalized;
    }
}
