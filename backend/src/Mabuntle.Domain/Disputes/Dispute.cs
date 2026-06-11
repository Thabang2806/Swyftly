using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Disputes;

public sealed class Dispute : AuditableEntity
{
    private readonly List<DisputeMessage> _messages = [];
    private readonly List<DisputeEvidence> _evidence = [];

    private Dispute()
    {
    }

    public Dispute(
        Guid orderId,
        Guid? returnRequestId,
        Guid buyerId,
        Guid sellerId,
        Guid openedByUserId,
        string reason,
        DateTimeOffset openedAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (openedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Opened-by user id is required.", nameof(openedByUserId));
        }

        OrderId = orderId;
        ReturnRequestId = returnRequestId == Guid.Empty ? null : returnRequestId;
        BuyerId = buyerId;
        SellerId = sellerId;
        OpenedByUserId = openedByUserId;
        Reason = Required(reason, nameof(reason), maxLength: 2000);
        Status = DisputeStatus.Open;
        OpenedAtUtc = openedAtUtc;
        CreatedAtUtc = openedAtUtc;
        UpdatedAtUtc = openedAtUtc;
        AddMessage(openedByUserId, "Buyer", Reason, openedAtUtc);
    }

    public Guid OrderId { get; private set; }

    public Guid? ReturnRequestId { get; private set; }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public DisputeStatus Status { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public string? ResolutionReason { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public IReadOnlyCollection<DisputeMessage> Messages => _messages.AsReadOnly();

    public IReadOnlyCollection<DisputeEvidence> Evidence => _evidence.AsReadOnly();

    public bool IsActive => Status is DisputeStatus.Open
        or DisputeStatus.AwaitingBuyer
        or DisputeStatus.AwaitingSeller
        or DisputeStatus.UnderAdminReview;

    public void AddMessage(Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        EnsureActive();
        _messages.Add(new DisputeMessage(Id, senderUserId, senderRole, message, createdAtUtc));
        UpdatedAtUtc = createdAtUtc;

        if (string.Equals(senderRole, "Buyer", StringComparison.OrdinalIgnoreCase))
        {
            Status = DisputeStatus.AwaitingSeller;
        }
        else if (string.Equals(senderRole, "Seller", StringComparison.OrdinalIgnoreCase))
        {
            Status = DisputeStatus.AwaitingBuyer;
        }
    }

    public void AddEvidence(
        Guid submittedByUserId,
        string submittedByRole,
        string evidenceType,
        string storageReference,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        EnsureActive();
        _evidence.Add(new DisputeEvidence(
            Id,
            submittedByUserId,
            submittedByRole,
            evidenceType,
            storageReference,
            description,
            createdAtUtc));
        UpdatedAtUtc = createdAtUtc;
    }

    public void MarkUnderAdminReview(DateTimeOffset changedAtUtc)
    {
        EnsureActive();
        Status = DisputeStatus.UnderAdminReview;
        UpdatedAtUtc = changedAtUtc;
    }

    public void ResolveBuyerFavoured(Guid resolvedByUserId, string reason, DateTimeOffset resolvedAtUtc)
    {
        Resolve(DisputeStatus.ResolvedBuyerFavoured, resolvedByUserId, reason, resolvedAtUtc);
    }

    public void ResolveSellerFavoured(Guid resolvedByUserId, string reason, DateTimeOffset resolvedAtUtc)
    {
        Resolve(DisputeStatus.ResolvedSellerFavoured, resolvedByUserId, reason, resolvedAtUtc);
    }

    private void Resolve(DisputeStatus status, Guid resolvedByUserId, string reason, DateTimeOffset resolvedAtUtc)
    {
        EnsureActive();
        if (resolvedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Resolved-by user id is required.", nameof(resolvedByUserId));
        }

        Status = status;
        ResolvedByUserId = resolvedByUserId;
        ResolutionReason = Required(reason, nameof(reason), maxLength: 2000);
        ResolvedAtUtc = resolvedAtUtc;
        UpdatedAtUtc = resolvedAtUtc;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Closed or resolved disputes cannot be changed.");
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
