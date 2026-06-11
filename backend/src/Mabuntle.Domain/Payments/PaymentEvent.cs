using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Payments;

public sealed class PaymentEvent : Entity
{
    private PaymentEvent()
    {
    }

    public PaymentEvent(
        Guid? paymentId,
        string provider,
        string providerEventId,
        string eventType,
        string rawPayloadJson,
        DateTimeOffset receivedAtUtc)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));
        }

        PaymentId = paymentId;
        Provider = Required(provider, nameof(provider));
        ProviderEventId = Required(providerEventId, nameof(providerEventId));
        EventType = Required(eventType, nameof(eventType));
        RawPayloadJson = Required(rawPayloadJson, nameof(rawPayloadJson));
        ReceivedAtUtc = receivedAtUtc;
        ProcessingStatus = PaymentEventProcessingStatus.Received;
    }

    public Guid? PaymentId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderEventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string RawPayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset? RawPayloadRedactedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public PaymentEventProcessingStatus ProcessingStatus { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void MarkProcessed(Guid paymentId, DateTimeOffset processedAtUtc)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        PaymentId = paymentId;
        ProcessingStatus = PaymentEventProcessingStatus.Processed;
        ProcessedAtUtc = processedAtUtc;
        ErrorMessage = null;
    }

    public void MarkDuplicate(DateTimeOffset processedAtUtc)
    {
        ProcessingStatus = PaymentEventProcessingStatus.Duplicate;
        ProcessedAtUtc = processedAtUtc;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset processedAtUtc)
    {
        ProcessingStatus = PaymentEventProcessingStatus.Failed;
        ProcessedAtUtc = processedAtUtc;
        ErrorMessage = Required(errorMessage, nameof(errorMessage));
    }

    public bool RedactRawPayload(string redactedPayloadJson, DateTimeOffset redactedAtUtc)
    {
        if (RawPayloadRedactedAtUtc is not null)
        {
            return false;
        }

        RawPayloadJson = Required(redactedPayloadJson, nameof(redactedPayloadJson));
        RawPayloadRedactedAtUtc = redactedAtUtc;
        return true;
    }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }
}
