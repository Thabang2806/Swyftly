namespace Mabuntle.Domain.Orders;

public enum ShipmentStatus
{
    AwaitingFulfilment = 0,
    Packed,
    ReadyForCourier,
    Collected,
    InTransit,
    Delivered,
    DeliveryFailed,
    ReturnedToSender
}
