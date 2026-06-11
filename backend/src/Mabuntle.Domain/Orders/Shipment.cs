using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Orders;

public sealed class Shipment : AuditableEntity
{
    public const int ExceptionReasonMaxLength = 500;
    public const int CarrierProviderNameMaxLength = 80;
    public const int CarrierServiceCodeMaxLength = 80;
    public const int ProviderShipmentReferenceMaxLength = 160;
    public const int ProviderStatusMaxLength = 80;
    public const int ProviderLabelUrlMaxLength = 500;
    public const int ProviderErrorMaxLength = 1000;

    private readonly List<ShipmentEvent> _events = [];

    private Shipment()
    {
    }

    public Shipment(Guid orderId, Guid sellerId, Guid buyerId, DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        OrderId = orderId;
        SellerId = sellerId;
        BuyerId = buyerId;
        Status = ShipmentStatus.AwaitingFulfilment;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        AddEvent("ShipmentCreated", "Shipment is awaiting fulfilment.", createdAtUtc);
    }

    public Guid OrderId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid BuyerId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public string? CarrierName { get; private set; }

    public string? TrackingNumber { get; private set; }

    public string? TrackingUrl { get; private set; }

    public DateTimeOffset? ShippedAtUtc { get; private set; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public string? CarrierProviderName { get; private set; }

    public string? CarrierServiceCode { get; private set; }

    public string? ProviderShipmentReference { get; private set; }

    public CarrierBookingStatus? CarrierBookingStatus { get; private set; }

    public string? ProviderStatus { get; private set; }

    public DateTimeOffset? ProviderStatusUpdatedAtUtc { get; private set; }

    public DateTimeOffset? ProviderLastSyncedAtUtc { get; private set; }

    public string? ProviderLabelUrl { get; private set; }

    public string? ProviderError { get; private set; }

    public decimal? PackageWeightKg { get; private set; }

    public decimal? PackageLengthCm { get; private set; }

    public decimal? PackageWidthCm { get; private set; }

    public decimal? PackageHeightCm { get; private set; }

    public DateTimeOffset? CarrierBookedAtUtc { get; private set; }

    public IReadOnlyCollection<ShipmentEvent> Events => _events.AsReadOnly();

    public void UpdateTracking(
        string carrierName,
        string trackingNumber,
        string? trackingUrl,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        CarrierName = RequiredText(carrierName, nameof(carrierName), maxLength: 120);
        TrackingNumber = RequiredText(trackingNumber, nameof(trackingNumber), maxLength: 160);
        TrackingUrl = OptionalText(trackingUrl, maxLength: 500);
        UpdatedAtUtc = occurredAtUtc;
        AddEvent("TrackingUpdated", note ?? "Tracking details were updated.", occurredAtUtc);
    }

    public void MarkReadyForCourier(DateTimeOffset readyAtUtc, string? note = null)
    {
        if (Status is ShipmentStatus.InTransit or ShipmentStatus.Delivered or ShipmentStatus.DeliveryFailed or ShipmentStatus.ReturnedToSender)
        {
            throw new InvalidOperationException("In-transit, delivered, failed, or returned shipments cannot be marked ready for courier.");
        }

        if (Status == ShipmentStatus.ReadyForCourier)
        {
            return;
        }

        Status = ShipmentStatus.ReadyForCourier;
        UpdatedAtUtc = readyAtUtc;
        AddEvent("ShipmentReadyForCourier", note ?? "Shipment was marked ready for courier.", readyAtUtc);
    }

    public void BookCarrier(
        string providerName,
        string serviceCode,
        string providerShipmentReference,
        string carrierName,
        string trackingNumber,
        string? trackingUrl,
        string? labelUrl,
        CarrierProviderShipmentStatus providerStatus,
        decimal packageWeightKg,
        decimal packageLengthCm,
        decimal packageWidthCm,
        decimal packageHeightCm,
        DateTimeOffset bookedAtUtc)
    {
        if (Status != ShipmentStatus.ReadyForCourier)
        {
            throw new InvalidOperationException("Only ready-for-courier shipments can be booked with a carrier.");
        }

        ValidatePackage(packageWeightKg, nameof(packageWeightKg));
        ValidatePackage(packageLengthCm, nameof(packageLengthCm));
        ValidatePackage(packageWidthCm, nameof(packageWidthCm));
        ValidatePackage(packageHeightCm, nameof(packageHeightCm));

        CarrierProviderName = RequiredText(providerName, nameof(providerName), CarrierProviderNameMaxLength);
        CarrierServiceCode = RequiredText(serviceCode, nameof(serviceCode), CarrierServiceCodeMaxLength);
        ProviderShipmentReference = RequiredText(providerShipmentReference, nameof(providerShipmentReference), ProviderShipmentReferenceMaxLength);
        CarrierName = RequiredText(carrierName, nameof(carrierName), maxLength: 120);
        TrackingNumber = RequiredText(trackingNumber, nameof(trackingNumber), maxLength: 160);
        TrackingUrl = OptionalText(trackingUrl, maxLength: 500);
        ProviderLabelUrl = OptionalText(labelUrl, ProviderLabelUrlMaxLength);
        CarrierBookingStatus = Mabuntle.Domain.Orders.CarrierBookingStatus.Booked;
        ProviderStatus = providerStatus.ToString();
        ProviderStatusUpdatedAtUtc = bookedAtUtc;
        ProviderLastSyncedAtUtc = bookedAtUtc;
        ProviderError = null;
        PackageWeightKg = packageWeightKg;
        PackageLengthCm = packageLengthCm;
        PackageWidthCm = packageWidthCm;
        PackageHeightCm = packageHeightCm;
        CarrierBookedAtUtc = bookedAtUtc;
        UpdatedAtUtc = bookedAtUtc;
        AddEvent("CarrierBooked", $"Carrier booking created with {CarrierProviderName}.", bookedAtUtc);
    }

    public void MarkCarrierBookingFailed(
        string providerName,
        string serviceCode,
        string error,
        decimal packageWeightKg,
        decimal packageLengthCm,
        decimal packageWidthCm,
        decimal packageHeightCm,
        DateTimeOffset failedAtUtc)
    {
        ValidatePackage(packageWeightKg, nameof(packageWeightKg));
        ValidatePackage(packageLengthCm, nameof(packageLengthCm));
        ValidatePackage(packageWidthCm, nameof(packageWidthCm));
        ValidatePackage(packageHeightCm, nameof(packageHeightCm));

        CarrierProviderName = RequiredText(providerName, nameof(providerName), CarrierProviderNameMaxLength);
        CarrierServiceCode = RequiredText(serviceCode, nameof(serviceCode), CarrierServiceCodeMaxLength);
        CarrierBookingStatus = Mabuntle.Domain.Orders.CarrierBookingStatus.Failed;
        ProviderError = RequiredText(error, nameof(error), ProviderErrorMaxLength);
        PackageWeightKg = packageWeightKg;
        PackageLengthCm = packageLengthCm;
        PackageWidthCm = packageWidthCm;
        PackageHeightCm = packageHeightCm;
        ProviderLastSyncedAtUtc = failedAtUtc;
        UpdatedAtUtc = failedAtUtc;
        AddEvent("CarrierBookingFailed", ProviderError, failedAtUtc);
    }

    public bool UpdateProviderStatus(
        CarrierProviderShipmentStatus providerStatus,
        DateTimeOffset statusUpdatedAtUtc,
        DateTimeOffset syncedAtUtc,
        string? providerError = null)
    {
        var nextStatus = providerStatus.ToString();
        var changed = !string.Equals(ProviderStatus, nextStatus, StringComparison.Ordinal)
            || ProviderStatusUpdatedAtUtc != statusUpdatedAtUtc;

        ProviderStatus = nextStatus;
        ProviderStatusUpdatedAtUtc = statusUpdatedAtUtc;
        ProviderLastSyncedAtUtc = syncedAtUtc;
        ProviderError = OptionalText(providerError, ProviderErrorMaxLength);
        UpdatedAtUtc = syncedAtUtc;
        return changed;
    }

    public void MarkCollected(DateTimeOffset collectedAtUtc, string? note = null)
    {
        if (Status == ShipmentStatus.Collected)
        {
            return;
        }

        if (Status is ShipmentStatus.InTransit or ShipmentStatus.Delivered or ShipmentStatus.DeliveryFailed or ShipmentStatus.ReturnedToSender)
        {
            throw new InvalidOperationException("In-transit, delivered, failed, or returned shipments cannot be marked collected.");
        }

        Status = ShipmentStatus.Collected;
        ShippedAtUtc ??= collectedAtUtc;
        UpdatedAtUtc = collectedAtUtc;
        AddEvent("CarrierCollected", note ?? "Carrier collected the shipment.", collectedAtUtc);
    }

    public void MarkInTransit(DateTimeOffset shippedAtUtc, string? note = null)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.DeliveryFailed or ShipmentStatus.ReturnedToSender)
        {
            throw new InvalidOperationException("Delivered, failed, or returned shipments cannot be marked as in transit.");
        }

        if (Status == ShipmentStatus.InTransit)
        {
            return;
        }

        Status = ShipmentStatus.InTransit;
        ShippedAtUtc ??= shippedAtUtc;
        UpdatedAtUtc = shippedAtUtc;
        AddEvent("ShipmentInTransit", note ?? "Shipment was marked as shipped.", shippedAtUtc);
    }

    public void MarkDelivered(DateTimeOffset deliveredAtUtc, string? note = null)
    {
        if (Status != ShipmentStatus.InTransit)
        {
            throw new InvalidOperationException("Only in-transit shipments can be marked as delivered.");
        }

        Status = ShipmentStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        UpdatedAtUtc = deliveredAtUtc;
        AddEvent("ShipmentDelivered", note ?? "Shipment was marked as delivered.", deliveredAtUtc);
    }

    public void MarkDeliveryFailed(string reason, DateTimeOffset failedAtUtc)
    {
        if (Status == ShipmentStatus.DeliveryFailed)
        {
            return;
        }

        if (Status != ShipmentStatus.InTransit)
        {
            throw new InvalidOperationException("Only in-transit shipments can be marked delivery failed.");
        }

        Status = ShipmentStatus.DeliveryFailed;
        UpdatedAtUtc = failedAtUtc;
        AddEvent("DeliveryFailed", RequiredText(reason, nameof(reason), ExceptionReasonMaxLength), failedAtUtc);
    }

    public void MarkReturnedToSender(string reason, DateTimeOffset returnedAtUtc)
    {
        if (Status == ShipmentStatus.ReturnedToSender)
        {
            return;
        }

        if (Status is not (ShipmentStatus.InTransit or ShipmentStatus.DeliveryFailed))
        {
            throw new InvalidOperationException("Only in-transit or failed shipments can be marked returned to sender.");
        }

        Status = ShipmentStatus.ReturnedToSender;
        UpdatedAtUtc = returnedAtUtc;
        AddEvent("ReturnedToSender", RequiredText(reason, nameof(reason), ExceptionReasonMaxLength), returnedAtUtc);
    }

    private void AddEvent(string eventType, string? message, DateTimeOffset occurredAtUtc)
    {
        _events.Add(new ShipmentEvent(
            Id,
            Status,
            eventType,
            message,
            CarrierName,
            TrackingNumber,
            occurredAtUtc));
    }

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

    private static void ValidatePackage(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Package value must be greater than zero.");
        }
    }
}
