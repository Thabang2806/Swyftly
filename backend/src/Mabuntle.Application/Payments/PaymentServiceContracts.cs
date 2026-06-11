using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Payments;

public interface IPaymentService
{
    Task<Result<PaymentInitiationResponse>> InitiatePaymentAsync(
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentWebhookProcessingResult>> ProcessWebhookAsync(
        ProcessPaymentWebhookRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPaymentWebhookPayloadRetentionService
{
    Task<PaymentWebhookPayloadRetentionResult> RedactExpiredPayloadsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record InitiatePaymentRequest(
    Guid BuyerId,
    Guid OrderId);

public sealed record PaymentInitiationResponse(
    Guid PaymentId,
    Guid OrderId,
    string Provider,
    string? ProviderReference,
    decimal Amount,
    string Currency,
    string Status,
    Uri? CheckoutUrl);

public sealed record ProcessPaymentWebhookRequest(
    string Provider,
    string Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed record PaymentWebhookProcessingResult(
    Guid PaymentEventId,
    Guid? PaymentId,
    string ProviderEventId,
    string ProcessingStatus,
    string PaymentStatus,
    string? OrderStatus);

public sealed record PaymentWebhookPayloadRetentionResult(
    int RedactedCount,
    DateTimeOffset CutoffUtc);
