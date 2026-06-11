using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Payments;

namespace Mabuntle.Infrastructure.Payments;

public sealed class FakePaymentProvider(
    IOptions<PaymentProviderOptions> options,
    TimeProvider timeProvider) : IPaymentProvider
{
    public const string Name = "Fake";
    public const string MetadataOutcomeKey = "fakePaymentOutcome";
    public const string HeaderSignatureKey = "X-Mabuntle-Fake-Signature";

    private readonly PaymentProviderOptions _options = options.Value;

    public string ProviderName => Name;

    public Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = request.Metadata.TryGetValue(MetadataOutcomeKey, out var metadataOutcome)
            ? metadataOutcome
            : _options.FakeOutcome;

        if (string.Equals(outcome, FakePaymentOutcomes.Failure, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<PaymentInitiationResult>.Failure(
                Error.Failure("Payments.FakeProviderFailed", "The fake payment provider was configured to fail.")));
        }

        var providerReference = $"fake_{request.OrderId:N}_{timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}";
        var checkoutUrl = AppendQuery(request.SuccessUrl, "providerReference", providerReference);
        var result = new PaymentInitiationResult(
            ProviderName,
            providerReference,
            checkoutUrl,
            "Initialized",
            timeProvider.GetUtcNow().AddMinutes(15),
            new Dictionary<string, string>
            {
                ["simulation"] = FakePaymentOutcomes.Success,
                ["currency"] = request.Currency,
                ["amount"] = request.Amount.ToString("0.00")
            });

        return Task.FromResult(Result<PaymentInitiationResult>.Success(result));
    }

    public Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            return Task.FromResult(Result<PaymentVerificationResult>.Failure(
                Error.Validation([
                    new("providerReference", "Provider reference is required.")
                ])));
        }

        var status = request.ProviderReference.Contains("failed", StringComparison.OrdinalIgnoreCase)
            ? "Failed"
            : "Paid";
        var result = new PaymentVerificationResult(
            ProviderName,
            request.ProviderReference,
            status,
            null,
            _options.DefaultCurrency,
            timeProvider.GetUtcNow());

        return Task.FromResult(Result<PaymentVerificationResult>.Success(result));
    }

    public Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
        PaymentWebhookParseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            return Task.FromResult(Result<PaymentWebhookEvent>.Failure(
                Error.Validation([
                    new("payload", "Webhook payload is required.")
                ])));
        }

        try
        {
            using var document = JsonDocument.Parse(request.Payload);
            var root = document.RootElement;
            var eventId = GetString(root, "eventId") ?? $"fake_evt_{Guid.NewGuid():N}";
            var eventType = GetString(root, "eventType") ?? "payment.updated";
            var providerReference = GetString(root, "providerReference") ?? string.Empty;
            var status = GetString(root, "status") ?? "Paid";
            var occurredAtUtc = GetDateTimeOffset(root, "occurredAtUtc") ?? timeProvider.GetUtcNow();
            var amount = GetDecimal(root, "amount");
            var currency = GetString(root, "currency");

            if (string.IsNullOrWhiteSpace(providerReference))
            {
                return Task.FromResult(Result<PaymentWebhookEvent>.Failure(
                    Error.Validation([
                        new("providerReference", "Webhook provider reference is required.")
                    ])));
            }

            return Task.FromResult(Result<PaymentWebhookEvent>.Success(new PaymentWebhookEvent(
                ProviderName,
                eventId,
                eventType,
                providerReference,
                status,
                occurredAtUtc,
                request.Payload,
                amount,
                currency)));
        }
        catch (JsonException)
        {
            return Task.FromResult(Result<PaymentWebhookEvent>.Failure(
                Error.Validation([
                    new("payload", "Webhook payload must be valid JSON.")
                ])));
        }
    }

    public Task<Result<PaymentRefundResult>> RefundPaymentAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderReference))
        {
            return Task.FromResult(Result<PaymentRefundResult>.Failure(
                Error.Validation([
                    new("providerReference", "Provider reference is required.")
                ])));
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(Result<PaymentRefundResult>.Failure(
                Error.Validation([
                    new("amount", "Refund amount must be positive.")
                ])));
        }

        if (request.ProviderReference.Contains("refund_failed", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<PaymentRefundResult>.Failure(
                Error.Failure("Payments.FakeRefundFailed", "The fake payment provider refund was configured to fail.")));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Task.FromResult(Result<PaymentRefundResult>.Failure(
                Error.Validation([
                    new("idempotencyKey", "Refund idempotency key is required.")
                ])));
        }

        var result = new PaymentRefundResult(
            ProviderName,
            $"fake_refund_{request.IdempotencyKey.Trim().ToLowerInvariant()}",
            "Refunded",
            request.Amount,
            request.Currency,
            timeProvider.GetUtcNow());

        return Task.FromResult(Result<PaymentRefundResult>.Success(result));
    }

    public Task<Result> VerifyWebhookSignatureAsync(
        PaymentWebhookSignatureVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret))
        {
            return Task.FromResult(Result.Failure(
                Error.Unauthorized(
                    "Payments.WebhookSigningSecretNotConfigured",
                    "Webhook signature verification is not configured.")));
        }

        var expectedSignature = ComputeSignature(request.Payload, _options.WebhookSigningSecret);
        var providedSignature = request.Headers.TryGetValue(HeaderSignatureKey, out var signature)
            ? signature
            : string.Empty;

        return string.Equals(expectedSignature, providedSignature, StringComparison.Ordinal)
            ? Task.FromResult(Result.Success())
            : Task.FromResult(Result.Failure(
                Error.Unauthorized("Payments.InvalidWebhookSignature", "Webhook signature is invalid.")));
    }

    private static Uri AppendQuery(Uri uri, string key, string value)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string propertyName)
    {
        var value = GetString(root, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
