namespace Mabuntle.Domain.Returns;

public enum ReturnStatus
{
    Requested = 0,
    AwaitingSellerResponse,
    Approved,
    Rejected,
    ReturnInTransit,
    ReturnedToSeller,
    RefundPending,
    Refunded,
    Disputed,
    Closed
}
