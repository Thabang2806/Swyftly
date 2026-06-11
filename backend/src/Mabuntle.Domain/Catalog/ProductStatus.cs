namespace Mabuntle.Domain.Catalog;

public enum ProductStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Rejected = 3,
    Archived = 4,
    OutOfStock = 5,
    NeedsAdminReview = 6,
    ChangesRequested = 7
}
