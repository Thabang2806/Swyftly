namespace Swyftly.Domain.Media;

public enum MediaAssetLifecycleStatus
{
    Stored = 0,
    PendingDeletion = 1,
    Deleted = 2,
    DeleteFailed = 3
}
