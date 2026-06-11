using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdCampaign : AuditableEntity
{
    private readonly List<AdCampaignProduct> _products = [];

    private AdCampaign()
    {
    }

    public AdCampaign(
        Guid sellerId,
        string name,
        AdCampaignType campaignType,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        Status = AdCampaignStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        UpdateDraft(name, campaignType, startsAtUtc, endsAtUtc, createdAtUtc);
    }

    public Guid SellerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AdCampaignType CampaignType { get; private set; }

    public AdCampaignStatus Status { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? PausedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<AdCampaignProduct> Products => _products.AsReadOnly();

    public bool CanSellerEdit => Status is AdCampaignStatus.Draft or AdCampaignStatus.Rejected;

    public void UpdateDraft(
        string name,
        AdCampaignType campaignType,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureSellerEditable();
        if (endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("Campaign end date must be after start date.", nameof(endsAtUtc));
        }

        Name = Required(name, nameof(name), 160);
        CampaignType = campaignType;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ReplaceProducts(IEnumerable<Guid> productIds, DateTimeOffset updatedAtUtc)
    {
        EnsureSellerEditable();

        var distinctProductIds = productIds
            .Where(productId => productId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctProductIds.Length == 0)
        {
            throw new ArgumentException("At least one product is required.", nameof(productIds));
        }

        _products.Clear();
        foreach (var productId in distinctProductIds)
        {
            _products.Add(new AdCampaignProduct(Id, productId, updatedAtUtc));
        }

        UpdatedAtUtc = updatedAtUtc;
    }

    public void SubmitForReview(DateTimeOffset submittedAtUtc)
    {
        if (!CanSellerEdit)
        {
            throw new InvalidOperationException("Only draft or rejected campaigns can be submitted for review.");
        }

        if (_products.Count == 0)
        {
            throw new InvalidOperationException("Campaign must have at least one product before review.");
        }

        Status = AdCampaignStatus.PendingReview;
        SubmittedAtUtc = submittedAtUtc;
        RejectionReason = null;
        UpdatedAtUtc = submittedAtUtc;
    }

    public void Approve(Guid approvedByUserId, DateTimeOffset approvedAtUtc)
    {
        if (Status != AdCampaignStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending-review campaigns can be approved.");
        }

        if (approvedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Approved-by user id is required.", nameof(approvedByUserId));
        }

        Status = AdCampaignStatus.Active;
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = approvedAtUtc;
        UpdatedAtUtc = approvedAtUtc;
    }

    public void Reject(string reason, DateTimeOffset rejectedAtUtc)
    {
        if (Status != AdCampaignStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending-review campaigns can be rejected.");
        }

        Status = AdCampaignStatus.Rejected;
        RejectionReason = Required(reason, nameof(reason), 1000);
        UpdatedAtUtc = rejectedAtUtc;
    }

    public void Pause(DateTimeOffset pausedAtUtc)
    {
        if (Status != AdCampaignStatus.Active)
        {
            throw new InvalidOperationException("Only active campaigns can be paused.");
        }

        Status = AdCampaignStatus.Paused;
        PausedAtUtc = pausedAtUtc;
        UpdatedAtUtc = pausedAtUtc;
    }

    public void Resume(DateTimeOffset resumedAtUtc)
    {
        if (Status != AdCampaignStatus.Paused)
        {
            throw new InvalidOperationException("Only paused campaigns can be resumed.");
        }

        Status = AdCampaignStatus.Active;
        UpdatedAtUtc = resumedAtUtc;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (Status is AdCampaignStatus.Cancelled or AdCampaignStatus.Completed)
        {
            return;
        }

        Status = AdCampaignStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        UpdatedAtUtc = completedAtUtc;
    }

    public void Cancel(DateTimeOffset cancelledAtUtc)
    {
        if (Status is AdCampaignStatus.Completed or AdCampaignStatus.Cancelled)
        {
            return;
        }

        Status = AdCampaignStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        UpdatedAtUtc = cancelledAtUtc;
    }

    private void EnsureSellerEditable()
    {
        if (!CanSellerEdit)
        {
            throw new InvalidOperationException("Seller can edit only draft or rejected campaigns.");
        }
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
