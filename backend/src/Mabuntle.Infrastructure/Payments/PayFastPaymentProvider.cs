using System.Globalization;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Payments;

namespace Mabuntle.Infrastructure.Payments;

public sealed class PayFastPaymentProvider(
    IOptions<PayFastOptions> options,
    HttpClient httpClient,
    TimeProvider timeProvider) : IPaymentProvider
{
    public const string Name = "PayFast";
    public const string ManualRefundRequiredCode = "Payments.PayFastManualRefundRequired";

    private readonly PayFastOptions _options = options.Value;

    public string ProviderName => Name;

    public Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateInitiationRequest(request);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<PaymentInitiationResult>.Failure(validation.Error));
        }

        var providerReference = ResolveProviderReference(request);
        var checkoutUrl = BuildCheckoutBridgeUrl(providerReference);
        if (checkoutUrl is null)
        {
            return Task.FromResult(Result<PaymentInitiationResult>.Failure(
                Error.Failure(
                    "Payments.PayFastCheckoutBridgeNotConfigured",
                    "PayFast checkout bridge base URL is not configured.")));
        }

        var result = new PaymentInitiationResult(
            ProviderName,
            providerReference,
            checkoutUrl,
            "Initialized",
            timeProvider.GetUtcNow().AddMinutes(30),
            new Dictionary<string, string>
            {
                ["currency"] = request.Currency.ToUpperInvariant(),
                ["amount"] = PayFastFormEncoder.FormatAmount(request.Amount),
                ["m_payment_id"] = providerReference
            });

        return Task.FromResult(Result<PaymentInitiationResult>.Success(result));
    }

    public Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<PaymentVerificationResult>.Failure(
            Error.Failure(
                "Payments.PayFastVerificationUnavailable",
                "PayFast status verification is not implemented in this adapter foundation.")));
    }

    public Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
        PaymentWebhookParseRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KeyValuePair<string, string>> pairs;
        try
        {
            pairs = PayFastFormEncoder.ParseOrderedPairs(request.Payload);
        }
        catch (UriFormatException)
        {
            return Task.FromResult(Result<PaymentWebhookEvent>.Failure(
                Error.Validation([
                    new ValidationFailure("payload", "PayFast ITN payload is not valid form data.")
                ])));
        }

        var values = PayFastFormEncoder.ToDictionary(pairs);
        var providerReference = GetRequired(values, "m_payment_id") ?? string.Empty;
        var eventId = GetRequired(values, "pf_payment_id") ?? string.Empty;
        var status = GetRequired(values, "payment_status") ?? string.Empty;

        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            failures.Add(new("m_payment_id", "PayFast merchant payment id is required."));
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            failures.Add(new("pf_payment_id", "PayFast payment id is required."));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            failures.Add(new("payment_status", "PayFast payment status is required."));
        }

        if (failures.Count > 0)
        {
            return Task.FromResult(Result<PaymentWebhookEvent>.Failure(Error.Validation(failures)));
        }

        decimal? amount = null;
        if (values.TryGetValue("amount_gross", out var amountGross)
            && decimal.TryParse(amountGross, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            amount = parsedAmount;
        }

        var occurredAtUtc = timeProvider.GetUtcNow();
        if (values.TryGetValue("payment_date", out var paymentDate)
            && DateTimeOffset.TryParse(paymentDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
        {
            occurredAtUtc = parsedDate.ToUniversalTime();
        }

        var normalizedStatus = NormalizeStatus(status);
        var eventType = $"payfast.{status.Trim().ToLowerInvariant()}";

        return Task.FromResult(Result<PaymentWebhookEvent>.Success(new PaymentWebhookEvent(
            ProviderName,
            eventId,
            eventType,
            providerReference,
            normalizedStatus,
            occurredAtUtc,
            request.Payload,
            amount,
            "ZAR")));
    }

    public Task<Result<PaymentRefundResult>> RefundPaymentAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<PaymentRefundResult>.Failure(
            Error.Conflict(
                ManualRefundRequiredCode,
                "PayFast refunds are manual in this phase. Confirm the dashboard refund after the provider refund reference is available.")));
    }

    public async Task<Result> VerifyWebhookSignatureAsync(
        PaymentWebhookSignatureVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KeyValuePair<string, string>> pairs;
        try
        {
            pairs = PayFastFormEncoder.ParseOrderedPairs(request.Payload);
        }
        catch (UriFormatException)
        {
            return Result.Failure(
                Error.Unauthorized("Payments.InvalidPayFastPayload", "PayFast ITN payload is invalid."));
        }

        var values = PayFastFormEncoder.ToDictionary(pairs);
        if (!values.TryGetValue(PayFastFormEncoder.SignatureFieldName, out var signature)
            || string.IsNullOrWhiteSpace(signature))
        {
            return Result.Failure(
                Error.Unauthorized("Payments.MissingPayFastSignature", "PayFast ITN signature is missing."));
        }

        var expectedSignature = PayFastFormEncoder.ComputeSignature(pairs, _options.Passphrase);
        if (!string.Equals(expectedSignature, signature.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return Result.Failure(
                Error.Unauthorized("Payments.InvalidPayFastSignature", "PayFast ITN signature is invalid."));
        }

        if (_options.RequireRemoteValidation)
        {
            if (string.IsNullOrWhiteSpace(_options.ValidateUrl))
            {
                return Result.Failure(
                    Error.Unauthorized("Payments.PayFastValidationUrlNotConfigured", "PayFast ITN remote validation URL is not configured."));
            }

            using var content = new StringContent(request.Payload);
            content.Headers.ContentType = new("application/x-www-form-urlencoded");

            using var response = await httpClient.PostAsync(_options.ValidateUrl, content, cancellationToken);
            var validationResponse = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (!response.IsSuccessStatusCode
                || !string.Equals(validationResponse, "VALID", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(
                    Error.Unauthorized("Payments.PayFastRemoteValidationFailed", "PayFast ITN remote validation failed."));
            }
        }

        return Result.Success();
    }

    private Result ValidateInitiationRequest(PaymentInitiationRequest request)
    {
        var failures = new List<ValidationFailure>();
        if (request.Amount <= 0)
        {
            failures.Add(new("amount", "Payment amount must be positive."));
        }

        if (!string.Equals(request.Currency, "ZAR", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(new("currency", "PayFast payments must use ZAR."));
        }

        if (string.IsNullOrWhiteSpace(_options.MerchantId))
        {
            failures.Add(new("merchantId", "PayFast merchant id is not configured."));
        }

        if (string.IsNullOrWhiteSpace(_options.MerchantKey))
        {
            failures.Add(new("merchantKey", "PayFast merchant key is not configured."));
        }

        if (string.IsNullOrWhiteSpace(_options.ProcessUrl))
        {
            failures.Add(new("processUrl", "PayFast process URL is not configured."));
        }

        if (failures.Count > 0)
        {
            return Result.Failure(Error.Validation(failures));
        }

        return Result.Success();
    }

    private static string ResolveProviderReference(PaymentInitiationRequest request)
    {
        if (request.Metadata.TryGetValue("paymentId", out var paymentIdValue)
            && Guid.TryParse(paymentIdValue, out var paymentId))
        {
            return paymentId.ToString("N");
        }

        return request.OrderId.ToString("N");
    }

    private Uri? BuildCheckoutBridgeUrl(string providerReference)
    {
        if (!Uri.TryCreate(_options.CheckoutBridgeBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        return new Uri(baseUri, $"/api/payments/payfast/checkout/{Uri.EscapeDataString(providerReference)}");
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToUpperInvariant() switch
        {
            "COMPLETE" => "Paid",
            "FAILED" => "Failed",
            "CANCELLED" or "CANCELED" => "Cancelled",
            var other => other
        };
    }

    private static string? GetRequired(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value.Trim() : null;
}
