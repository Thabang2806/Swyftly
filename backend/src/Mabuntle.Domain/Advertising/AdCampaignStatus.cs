namespace Mabuntle.Domain.Advertising;

public enum AdCampaignStatus
{
    Draft = 0,
    PendingReview,
    Active,
    Paused,
    Completed,
    Rejected,
    Cancelled
}
