namespace Mabuntle.Domain.Payments;

public enum PaymentStatus
{
    Pending = 0,
    Authorized,
    Paid,
    Failed,
    Cancelled,
    Refunded,
    PartiallyRefunded,
    Disputed
}
