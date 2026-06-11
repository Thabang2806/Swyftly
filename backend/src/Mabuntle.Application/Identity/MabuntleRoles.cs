namespace Mabuntle.Application.Identity;

public static class MabuntleRoles
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string SupportAgent = "SupportAgent";
    public const string FinanceOperator = "FinanceOperator";
    public const string FinanceApprover = "FinanceApprover";

    public static readonly IReadOnlySet<string> PublicRegistrationRoles = new HashSet<string>(
        [Buyer, Seller],
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyCollection<string> All =
    [
        Buyer,
        Seller,
        Admin,
        SuperAdmin,
        SupportAgent,
        FinanceOperator,
        FinanceApprover
    ];
}
