namespace Mabuntle.Domain.Disputes;

public enum DisputeStatus
{
    Open = 0,
    AwaitingBuyer,
    AwaitingSeller,
    UnderAdminReview,
    ResolvedBuyerFavoured,
    ResolvedSellerFavoured,
    Closed
}
