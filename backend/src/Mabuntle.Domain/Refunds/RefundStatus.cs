namespace Mabuntle.Domain.Refunds;

public enum RefundStatus
{
    Requested = 0,
    Approved,
    Processing,
    Refunded,
    Failed,
    Rejected
}
