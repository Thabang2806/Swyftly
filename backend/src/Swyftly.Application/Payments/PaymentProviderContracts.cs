using Swyftly.Application.Common.Results;

namespace Swyftly.Application.Payments;

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
        PaymentWebhookParseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentRefundResult>> RefundPaymentAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> VerifyWebhookSignatureAsync(
        PaymentWebhookSignatureVerificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentInitiationRequest(
    Guid OrderId,
    Guid BuyerId,
    decimal Amount,
    string Currency,
    string Description,
    Uri SuccessUrl,
    Uri FailureUrl,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PaymentInitiationResult(
    string Provider,
    string ProviderReference,
    Uri? CheckoutUrl,
    string Status,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyDictionary<string, string> ProviderMetadata);

public sealed record PaymentVerificationRequest(
    string ProviderReference);

public sealed record PaymentVerificationResult(
    string Provider,
    string ProviderReference,
    string Status,
    decimal? Amount,
    string? Currency,
    DateTimeOffset VerifiedAtUtc);

public sealed record PaymentWebhookParseRequest(
    string Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed record PaymentWebhookSignatureVerificationRequest(
    string Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed record PaymentWebhookEvent(
    string Provider,
    string EventId,
    string EventType,
    string ProviderReference,
    string Status,
    DateTimeOffset OccurredAtUtc,
    string Payload,
    decimal? Amount = null,
    string? Currency = null);

public sealed record PaymentRefundRequest(
    string ProviderReference,
    decimal Amount,
    string Currency,
    string Reason,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PaymentRefundResult(
    string Provider,
    string ProviderRefundReference,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset RefundedAtUtc);

public sealed class PaymentProviderOptions
{
    public const string SectionName = "PaymentProvider";

    public string ProviderName { get; set; } = "Fake";

    public string DefaultCurrency { get; set; } = "ZAR";

    public string SuccessRedirectUrl { get; set; } = "http://localhost:4200/checkout/success";

    public string FailureRedirectUrl { get; set; } = "http://localhost:4200/checkout/failed";

    public string WebhookSigningSecret { get; set; } = string.Empty;

    public string FakeOutcome { get; set; } = FakePaymentOutcomes.Success;
}

public static class FakePaymentOutcomes
{
    public const string Success = "Success";

    public const string Failure = "Failure";
}

public static class PaymentProviderNames
{
    public const string Disabled = "Disabled";
}
