namespace Swyftly.Domain.Buyers;

public static class BuyerNotificationCategory
{
    public const string Orders = "Orders";
    public const string Returns = "Returns";
    public const string Reviews = "Reviews";
    public const string Support = "Support";

    public static readonly IReadOnlyCollection<string> All =
    [
        Orders,
        Returns,
        Reviews,
        Support
    ];

    public static bool IsSupported(string category) =>
        All.Contains(category, StringComparer.Ordinal);
}
