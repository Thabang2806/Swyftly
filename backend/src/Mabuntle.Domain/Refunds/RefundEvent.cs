using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Refunds;

public sealed class RefundEvent : Entity
{
    private RefundEvent()
    {
    }

    public RefundEvent(
        Guid refundId,
        RefundStatus status,
        string eventType,
        string message,
        DateTimeOffset createdAtUtc)
    {
        if (refundId == Guid.Empty)
        {
            throw new ArgumentException("Refund id is required.", nameof(refundId));
        }

        RefundId = refundId;
        Status = status;
        EventType = Required(eventType, nameof(eventType));
        Message = Required(message, nameof(message));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid RefundId { get; private set; }

    public RefundStatus Status { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

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
