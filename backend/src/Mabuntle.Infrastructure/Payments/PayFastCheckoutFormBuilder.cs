using System.Net;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Payments;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;

namespace Mabuntle.Infrastructure.Payments;

public sealed class PayFastCheckoutFormBuilder(
    IOptions<PayFastOptions> options,
    IOptions<PaymentProviderOptions> paymentOptions)
{
    private readonly PayFastOptions _options = options.Value;
    private readonly PaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public Result<PayFastCheckoutForm> Build(Payment payment, Order order)
    {
        if (!string.Equals(payment.Provider, PayFastPaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PayFastCheckoutForm>.Failure(
                Error.Conflict("Payments.NotPayFastPayment", "The payment was not created for PayFast."));
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return Result<PayFastCheckoutForm>.Failure(
                Error.Conflict("Payments.PayFastPaymentNotPending", "Only pending PayFast payments can be checked out."));
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderReference))
        {
            return Result<PayFastCheckoutForm>.Failure(
                Error.Conflict("Payments.MissingPayFastReference", "The PayFast payment reference is missing."));
        }

        var processUrl = ResolveProcessUrl();
        if (processUrl is null)
        {
            return Result<PayFastCheckoutForm>.Failure(
                Error.Failure("Payments.PayFastProcessUrlNotConfigured", "PayFast process URL is not configured."));
        }

        var fields = new List<KeyValuePair<string, string>>
        {
            new("merchant_id", _options.MerchantId),
            new("merchant_key", _options.MerchantKey),
            new("return_url", AppendOrderId(_paymentOptions.SuccessRedirectUrl, order.Id)),
            new("cancel_url", AppendOrderId(_paymentOptions.FailureRedirectUrl, order.Id)),
            new("notify_url", _options.NotifyUrl),
            new("m_payment_id", payment.ProviderReference),
            new("amount", PayFastFormEncoder.FormatAmount(payment.Amount)),
            new("item_name", $"Mabuntle order {order.Id:N}"),
            new("item_description", $"Mabuntle marketplace order {order.Id:N}"),
            new("custom_str1", order.Id.ToString("N")),
            new("custom_str2", payment.Id.ToString("N"))
        };

        fields.Add(new(PayFastFormEncoder.SignatureFieldName, PayFastFormEncoder.ComputeSignature(fields, _options.Passphrase)));

        var html = BuildAutoSubmitHtml(processUrl, fields);
        return Result<PayFastCheckoutForm>.Success(new PayFastCheckoutForm(processUrl, fields, html));
    }

    private Uri? ResolveProcessUrl() =>
        Uri.TryCreate(_options.ProcessUrl, UriKind.Absolute, out var processUrl)
            ? processUrl
            : null;

    private static string AppendOrderId(string url, Guid orderId)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return $"{uri}{separator}orderId={Uri.EscapeDataString(orderId.ToString("N"))}";
    }

    private static string BuildAutoSubmitHtml(
        Uri processUrl,
        IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var inputs = string.Join(Environment.NewLine, fields.Select(field =>
            $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(field.Key)}\" value=\"{WebUtility.HtmlEncode(field.Value)}\" />"));

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="robots" content="noindex,nofollow" />
  <title>Redirecting to PayFast</title>
</head>
<body>
  <form id="payfast-checkout" method="post" action="{{WebUtility.HtmlEncode(processUrl.ToString())}}">
{{inputs}}
    <noscript>
      <button type="submit">Continue to PayFast</button>
    </noscript>
  </form>
  <script>
    document.getElementById('payfast-checkout').submit();
  </script>
</body>
</html>
""";
    }
}

public sealed record PayFastCheckoutForm(
    Uri ProcessUrl,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    string Html);
