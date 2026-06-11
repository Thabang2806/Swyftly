namespace Mabuntle.Domain.Ledger;

public enum LedgerEntryType
{
    BuyerPaymentReceived = 0,
    PlatformCommissionRecorded,
    PaymentProviderFeeRecorded,
    SellerPendingBalanceCredited,
    SellerBalanceHeld,
    SellerBalanceAvailable,
    SellerPayoutReleased,
    RefundIssued,
    RefundReversal,
    ManualAdjustment
}
