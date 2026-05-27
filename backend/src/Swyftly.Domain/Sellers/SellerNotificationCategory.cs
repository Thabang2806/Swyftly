namespace Swyftly.Domain.Sellers;

public static class SellerNotificationCategory
{
    public const string Verification = "Verification";
    public const string Products = "Products";
    public const string Revisions = "Revisions";
    public const string Ads = "Ads";
    public const string Reports = "Reports";

    public static readonly IReadOnlyCollection<string> All =
    [
        Verification,
        Products,
        Revisions,
        Ads,
        Reports
    ];

    public static bool IsSupported(string category) =>
        All.Contains(category, StringComparer.Ordinal);
}
