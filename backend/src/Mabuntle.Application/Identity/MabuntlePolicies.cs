namespace Mabuntle.Application.Identity;

public static class MabuntlePolicies
{
    public const string BuyerOnly = "BuyerOnly";
    public const string SellerOnly = "SellerOnly";
    public const string BuyerOrSeller = "BuyerOrSeller";
    public const string AdminOnly = "AdminOnly";
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string SupportAgentOnly = "SupportAgentOnly";
    public const string FinanceRead = "FinanceRead";
    public const string FinanceOperate = "FinanceOperate";
    public const string FinanceApprove = "FinanceApprove";
}
