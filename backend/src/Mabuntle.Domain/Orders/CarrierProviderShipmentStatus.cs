namespace Mabuntle.Domain.Orders;

public enum CarrierProviderShipmentStatus
{
    Booked = 0,
    LabelCreated = 1,
    Collected = 2,
    InTransit = 3,
    Delivered = 4,
    DeliveryFailed = 5,
    ReturnedToSender = 6
}
