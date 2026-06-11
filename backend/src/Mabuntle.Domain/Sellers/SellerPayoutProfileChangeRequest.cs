using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerPayoutProfileChangeRequest : AuditableEntity
{
    public const int PayoutProviderReferenceMaxLength = 256;
    public const int ReasonMaxLength = 1000;

    private SellerPayoutProfileChangeRequest()
    {
    }

    public SellerPayoutProfileChangeRequest(
        Guid sellerId,
        string proposedPayoutProviderReference,
        string reason,
        Guid requestedByUserId)
    {
        SellerId = sellerId;
        RequestedByUserId = requestedByUserId;
        Status = SellerPayoutProfileChangeRequestStatus.Draft;
        ProposedPayoutProviderReference = Required(
            proposedPayoutProviderReference,
            nameof(proposedPayoutProviderReference),
            PayoutProviderReferenceMaxLength);
        Reason = Required(reason, nameof(reason), ReasonMaxLength);
    }

    public Guid SellerId { get; private set; }

    public string ProposedPayoutProviderReference { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public SellerPayoutProfileChangeRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public string? ReviewReason { get; private set; }

    public int ConcurrencyVersion { get; private set; }

    public bool IsActive =>
        Status is SellerPayoutProfileChangeRequestStatus.Draft
            or SellerPayoutProfileChangeRequestStatus.PendingReview;

    public void UpdateDraft(string proposedPayoutProviderReference, string reason)
    {
        if (Status != SellerPayoutProfileChangeRequestStatus.Draft)
        {
            throw new InvalidOperationException("Only draft payout profile change requests can be updated.");
        }

        ProposedPayoutProviderReference = Required(
            proposedPayoutProviderReference,
            nameof(proposedPayoutProviderReference),
            PayoutProviderReferenceMaxLength);
        Reason = Required(reason, nameof(reason), ReasonMaxLength);
        ConcurrencyVersion++;
    }

    public void Submit(DateTimeOffset submittedAtUtc)
    {
        if (Status != SellerPayoutProfileChangeRequestStatus.Draft)
        {
            throw new InvalidOperationException("Only draft payout profile change requests can be submitted for review.");
        }

        Status = SellerPayoutProfileChangeRequestStatus.PendingReview;
        SubmittedAtUtc = submittedAtUtc;
        ConcurrencyVersion++;
    }

    public void Cancel(DateTimeOffset cancelledAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Only draft or pending payout profile change requests can be cancelled.");
        }

        Status = SellerPayoutProfileChangeRequestStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        ConcurrencyVersion++;
    }

    public void Approve(Guid reviewedByUserId, string reason, DateTimeOffset reviewedAtUtc)
    {
        EnsureCanReview(reviewedByUserId);
        ReviewReason = Required(reason, nameof(reason), ReasonMaxLength);
        Status = SellerPayoutProfileChangeRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ConcurrencyVersion++;
    }

    public void Reject(Guid reviewedByUserId, string reason, DateTimeOffset reviewedAtUtc)
    {
        EnsureCanReview(reviewedByUserId);
        ReviewReason = Required(reason, nameof(reason), ReasonMaxLength);
        Status = SellerPayoutProfileChangeRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ConcurrencyVersion++;
    }

    private void EnsureCanReview(Guid reviewedByUserId)
    {
        if (Status != SellerPayoutProfileChangeRequestStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending payout profile change requests can be reviewed.");
        }

        if (reviewedByUserId == RequestedByUserId)
        {
            throw new InvalidOperationException("The user who requested a payout profile change cannot approve or reject it.");
        }
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
