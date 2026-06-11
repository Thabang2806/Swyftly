using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerVerification : AuditableEntity
{
    private SellerVerification()
    {
    }

    public SellerVerification(Guid sellerId, DateTimeOffset submittedAtUtc)
    {
        SellerId = sellerId;
        SubmittedAtUtc = submittedAtUtc;
        Status = SellerVerificationStatus.UnderReview;
    }

    public Guid SellerId { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public SellerVerificationStatus Status { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public string? RejectionReason { get; private set; }

    public void Approve(Guid reviewedByUserId, DateTimeOffset reviewedAtUtc)
    {
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = null;
        Status = SellerVerificationStatus.Verified;
    }

    public void Reject(Guid reviewedByUserId, DateTimeOffset reviewedAtUtc, string reason)
    {
        var trimmedReason = reason.Trim();

        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(reason));
        }

        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = trimmedReason;
        Status = SellerVerificationStatus.Rejected;
    }
}
