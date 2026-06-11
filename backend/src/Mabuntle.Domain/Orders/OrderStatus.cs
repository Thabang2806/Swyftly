namespace Mabuntle.Domain.Orders;

public enum OrderStatus
{
    PendingPayment = 0,
    Paid,
    Processing,
    ReadyToShip,
    Shipped,
    Delivered,
    ReturnRequested,
    Refunded,
    Cancelled,
    Disputed,
    Completed
}
