using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerPayoutProfilePlaceholder : AuditableEntity
{
    private SellerPayoutProfilePlaceholder()
    {
    }

    public SellerPayoutProfilePlaceholder(
        Guid sellerId,
        string payoutProviderReference)
    {
        SellerId = sellerId;
        PayoutProviderReference = Required(payoutProviderReference, nameof(payoutProviderReference));
        HasSubmittedPlaceholder = true;
        IsAdminApproved = false;
    }

    public Guid SellerId { get; private set; }

    public string PayoutProviderReference { get; private set; } = string.Empty;

    public bool HasSubmittedPlaceholder { get; private set; }

    public bool IsAdminApproved { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public void UpdateProviderReference(string payoutProviderReference)
    {
        PayoutProviderReference = Required(payoutProviderReference, nameof(payoutProviderReference));
        HasSubmittedPlaceholder = true;
        IsAdminApproved = false;
        ApprovedAtUtc = null;
        ApprovedByUserId = null;
    }

    public void MarkAdminApproved(Guid approvedByUserId, DateTimeOffset approvedAtUtc)
    {
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = approvedAtUtc;
        IsAdminApproved = true;
    }

    public void ReplaceProviderReferenceAndApprove(
        string payoutProviderReference,
        Guid approvedByUserId,
        DateTimeOffset approvedAtUtc)
    {
        PayoutProviderReference = Required(payoutProviderReference, nameof(payoutProviderReference));
        HasSubmittedPlaceholder = true;
        MarkAdminApproved(approvedByUserId, approvedAtUtc);
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
}
