namespace Mabuntle.Infrastructure.Payments;

public sealed class PayFastOptions
{
    public const string SectionName = "PayFast";

    public string MerchantId { get; set; } = string.Empty;

    public string MerchantKey { get; set; } = string.Empty;

    public string Passphrase { get; set; } = string.Empty;

    public string ProcessUrl { get; set; } = "https://sandbox.payfast.co.za/eng/process";

    public string ValidateUrl { get; set; } = "https://sandbox.payfast.co.za/eng/query/validate";

    public string NotifyUrl { get; set; } = "https://localhost:7268/api/payments/webhook/payfast";

    public string CheckoutBridgeBaseUrl { get; set; } = "https://localhost:7268";

    public bool RequireRemoteValidation { get; set; } = true;
}
