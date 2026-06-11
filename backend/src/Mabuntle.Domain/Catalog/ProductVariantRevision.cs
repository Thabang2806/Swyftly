using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class ProductVariantRevision : AuditableEntity
{
    private ProductVariantRevision()
    {
    }

    public ProductVariantRevision(Guid productId, Guid sellerId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        ProductId = productId;
        SellerId = sellerId;
        Status = ProductVariantRevisionStatus.Draft;
    }

    public Guid ProductId { get; private set; }

    public Guid SellerId { get; private set; }

    public ProductVariantRevisionStatus Status { get; private set; }

    public string? SellerReason { get; private set; }

    public string? RejectionReason { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public bool CanSellerEdit => Status is ProductVariantRevisionStatus.Draft or ProductVariantRevisionStatus.Rejected;

    public void UpdateSellerReason(string? sellerReason)
    {
        EnsureSellerEditable();
        SellerReason = TrimOrNull(sellerReason);

        if (Status == ProductVariantRevisionStatus.Rejected)
        {
            Status = ProductVariantRevisionStatus.Draft;
            RejectionReason = null;
            ReviewedByUserId = null;
            ReviewedAtUtc = null;
        }
    }

    public void SubmitForReview(bool hasAtLeastOneItem, DateTimeOffset submittedAtUtc)
    {
        EnsureSellerEditable();

        if (!hasAtLeastOneItem)
        {
            throw new InvalidOperationException("At least one staged variant change is required before review submission.");
        }

        Status = ProductVariantRevisionStatus.PendingReview;
        RejectionReason = null;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void Approve(Guid reviewedByUserId, DateTimeOffset reviewedAtUtc)
    {
        if (Status != ProductVariantRevisionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending variant revisions can be approved.");
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        }

        Status = ProductVariantRevisionStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = null;
    }

    public void Reject(string reason, Guid reviewedByUserId, DateTimeOffset reviewedAtUtc)
    {
        if (Status != ProductVariantRevisionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending variant revisions can be rejected.");
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        }

        Status = ProductVariantRevisionStatus.Rejected;
        RejectionReason = Required(reason, nameof(reason));
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public void Cancel()
    {
        if (Status is ProductVariantRevisionStatus.Approved or ProductVariantRevisionStatus.Cancelled)
        {
            return;
        }

        Status = ProductVariantRevisionStatus.Cancelled;
    }

    private void EnsureSellerEditable()
    {
        if (!CanSellerEdit)
        {
            throw new InvalidOperationException("Only draft or rejected variant revisions can be edited.");
        }
    }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
