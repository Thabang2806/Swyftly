using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Returns;

public sealed class ReturnRequest : AuditableEntity
{
    private readonly List<ReturnItem> _items = [];
    private readonly List<ReturnMessage> _messages = [];

    private ReturnRequest()
    {
    }

    public ReturnRequest(
        Guid orderId,
        Guid buyerId,
        Guid sellerId,
        ReturnReason reason,
        string? details,
        DateTimeOffset requestedAtUtc)
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

        OrderId = orderId;
        BuyerId = buyerId;
        SellerId = sellerId;
        Reason = reason;
        Details = OptionalText(details, maxLength: 2000);
        Status = ReturnStatus.Requested;
        RequestedAtUtc = requestedAtUtc;
        CreatedAtUtc = requestedAtUtc;
        UpdatedAtUtc = requestedAtUtc;
    }

    public Guid OrderId { get; private set; }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public ReturnStatus Status { get; private set; }

    public ReturnReason Reason { get; private set; }

    public string? Details { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public DateTimeOffset? SellerRespondedAtUtc { get; private set; }

    public Guid? SellerRespondedByUserId { get; private set; }

    public string? SellerResponseReason { get; private set; }

    public DateTimeOffset? DisputedAtUtc { get; private set; }

    public Guid? DisputedByUserId { get; private set; }

    public string? DisputeReason { get; private set; }

    public IReadOnlyCollection<ReturnItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<ReturnMessage> Messages => _messages.AsReadOnly();

    public void AddItem(
        Guid orderItemId,
        Guid productId,
        Guid productVariantId,
        int quantity,
        ReturnReason reason,
        bool isOpenedOrUnsealed,
        string? note)
    {
        if (Status != ReturnStatus.Requested)
        {
            throw new InvalidOperationException("Items can only be added while a return is being requested.");
        }

        if (_items.Any(item => item.OrderItemId == orderItemId))
        {
            throw new InvalidOperationException("An order item can only be added to a return once.");
        }

        _items.Add(new ReturnItem(
            Id,
            orderItemId,
            productId,
            productVariantId,
            quantity,
            reason,
            isOpenedOrUnsealed,
            note));
    }

    public void MarkAwaitingSellerResponse(DateTimeOffset changedAtUtc)
    {
        if (Status != ReturnStatus.Requested)
        {
            throw new InvalidOperationException("Only requested returns can wait for seller response.");
        }

        Status = ReturnStatus.AwaitingSellerResponse;
        UpdatedAtUtc = changedAtUtc;
    }

    public void Approve(Guid sellerUserId, string? message, DateTimeOffset respondedAtUtc)
    {
        EnsureAwaitingSellerResponse();
        if (sellerUserId == Guid.Empty)
        {
            throw new ArgumentException("Seller user id is required.", nameof(sellerUserId));
        }

        Status = ReturnStatus.Approved;
        SellerRespondedByUserId = sellerUserId;
        SellerRespondedAtUtc = respondedAtUtc;
        SellerResponseReason = OptionalText(message, maxLength: 2000);
        UpdatedAtUtc = respondedAtUtc;
        AddMessage(sellerUserId, "Seller", message ?? "Return approved.", respondedAtUtc);
    }

    public void Reject(Guid sellerUserId, string reason, DateTimeOffset respondedAtUtc)
    {
        EnsureAwaitingSellerResponse();
        if (sellerUserId == Guid.Empty)
        {
            throw new ArgumentException("Seller user id is required.", nameof(sellerUserId));
        }

        Status = ReturnStatus.Rejected;
        SellerRespondedByUserId = sellerUserId;
        SellerRespondedAtUtc = respondedAtUtc;
        SellerResponseReason = RequiredText(reason, nameof(reason), maxLength: 2000);
        UpdatedAtUtc = respondedAtUtc;
        AddMessage(sellerUserId, "Seller", reason, respondedAtUtc);
    }

    public void Dispute(Guid buyerUserId, string reason, DateTimeOffset disputedAtUtc)
    {
        if (Status != ReturnStatus.Rejected)
        {
            throw new InvalidOperationException("Only rejected returns can be disputed by the buyer.");
        }

        if (buyerUserId == Guid.Empty)
        {
            throw new ArgumentException("Buyer user id is required.", nameof(buyerUserId));
        }

        Status = ReturnStatus.Disputed;
        DisputedByUserId = buyerUserId;
        DisputedAtUtc = disputedAtUtc;
        DisputeReason = RequiredText(reason, nameof(reason), maxLength: 2000);
        UpdatedAtUtc = disputedAtUtc;
        AddMessage(buyerUserId, "Buyer", reason, disputedAtUtc);
    }

    public void MarkRefundPending(DateTimeOffset changedAtUtc)
    {
        if (Status is not (ReturnStatus.Approved or ReturnStatus.ReturnedToSeller or ReturnStatus.Disputed))
        {
            throw new InvalidOperationException("Only approved, returned, or disputed returns can move to refund pending.");
        }

        Status = ReturnStatus.RefundPending;
        UpdatedAtUtc = changedAtUtc;
    }

    public void MarkRefunded(DateTimeOffset changedAtUtc)
    {
        if (Status != ReturnStatus.RefundPending)
        {
            throw new InvalidOperationException("Only refund-pending returns can be marked refunded.");
        }

        Status = ReturnStatus.Refunded;
        UpdatedAtUtc = changedAtUtc;
    }

    public void AddMessage(Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        _messages.Add(new ReturnMessage(
            Id,
            senderUserId,
            senderRole,
            message,
            createdAtUtc));
    }

    private void EnsureAwaitingSellerResponse()
    {
        if (Status is not (ReturnStatus.Requested or ReturnStatus.AwaitingSellerResponse))
        {
            throw new InvalidOperationException("Only returns awaiting seller response can be approved or rejected.");
        }
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
