namespace Mabuntle.Domain.Ledger;

public enum SellerPayoutStatus
{
    Pending = 0,
    OnHold,
    Available,
    Processing,
    PaidOut,
    Reversed,
    Failed
}
