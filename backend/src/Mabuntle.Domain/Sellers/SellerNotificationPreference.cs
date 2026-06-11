using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerNotificationPreference : AuditableEntity
{
    private SellerNotificationPreference()
    {
        Category = string.Empty;
    }

    public SellerNotificationPreference(Guid sellerId, string category, bool isEnabled, bool emailEnabled = true)
    {
        SellerId = sellerId;
        Category = RequiredCategory(category);
        IsEnabled = isEnabled;
        EmailEnabled = emailEnabled;
    }

    public Guid SellerId { get; private set; }

    public string Category { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public bool EmailEnabled { get; private set; } = true;

    public void SetChannels(bool isEnabled, bool emailEnabled)
    {
        IsEnabled = isEnabled;
        EmailEnabled = emailEnabled;
    }

    private static string RequiredCategory(string category)
    {
        var normalized = category.Trim();
        if (!SellerNotificationCategory.IsSupported(normalized))
        {
            throw new ArgumentException($"Notification category '{category}' is not supported.", nameof(category));
        }

        return normalized;
    }
}
