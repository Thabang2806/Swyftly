using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Orders;

public sealed class ShipmentEvent : Entity
{
    private ShipmentEvent()
    {
    }

    public ShipmentEvent(
        Guid shipmentId,
        ShipmentStatus status,
        string eventType,
        string? message,
        string? carrierName,
        string? trackingNumber,
        DateTimeOffset occurredAtUtc)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id is required.", nameof(shipmentId));
        }

        ShipmentId = shipmentId;
        Status = status;
        EventType = RequiredText(eventType, nameof(eventType), maxLength: 120);
        Message = OptionalText(message, maxLength: 1000);
        CarrierName = OptionalText(carrierName, maxLength: 120);
        TrackingNumber = OptionalText(trackingNumber, maxLength: 160);
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid ShipmentId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string? Message { get; private set; }

    public string? CarrierName { get; private set; }

    public string? TrackingNumber { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
