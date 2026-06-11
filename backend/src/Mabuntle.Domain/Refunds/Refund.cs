using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Refunds;

public sealed class Refund : AuditableEntity
{
    private readonly List<RefundEvent> _events = [];

    private Refund()
    {
    }

    public Refund(
        Guid orderId,
        Guid paymentId,
        Guid buyerId,
        Guid sellerId,
        Guid? returnRequestId,
        decimal amount,
        string currency,
        string reason,
        DateTimeOffset requestedAtUtc)
        : this(
            orderId,
            paymentId,
            buyerId,
            sellerId,
            returnRequestId,
            amount,
            currency,
            reason,
            Guid.NewGuid(),
            "Admin",
            requestedAtUtc)
    {
    }

    public Refund(
        Guid orderId,
        Guid paymentId,
        Guid buyerId,
        Guid sellerId,
        Guid? returnRequestId,
        decimal amount,
        string currency,
        string reason,
        Guid requestedByUserId,
        string requestedByRole,
        DateTimeOffset requestedAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Requested-by user id is required.", nameof(requestedByUserId));
        }

        OrderId = orderId;
        PaymentId = paymentId;
        BuyerId = buyerId;
        SellerId = sellerId;
        ReturnRequestId = returnRequestId == Guid.Empty ? null : returnRequestId;
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Reason = Required(reason, nameof(reason));
        RequestedByUserId = requestedByUserId;
        RequestedByRole = Required(requestedByRole, nameof(requestedByRole));
        Status = RefundStatus.Requested;
        RequestedAtUtc = requestedAtUtc;
        CreatedAtUtc = requestedAtUtc;
        UpdatedAtUtc = requestedAtUtc;
        AddEvent(RefundStatus.Requested, "RefundRequested", reason, requestedAtUtc);
    }

    public Guid OrderId { get; private set; }

    public Guid PaymentId { get; private set; }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid? ReturnRequestId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public RefundStatus Status { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid? ApprovedByUserId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public string RequestedByRole { get; private set; } = string.Empty;

    public string? ApprovalReason { get; private set; }

    public string? ProviderRefundReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset? RefundedAtUtc { get; private set; }

    public int ConcurrencyVersion { get; private set; }

    public IReadOnlyCollection<RefundEvent> Events => _events.AsReadOnly();

    public void Approve(Guid approvedByUserId, string approvalReason, DateTimeOffset approvedAtUtc)
    {
        if (Status != RefundStatus.Requested)
        {
            throw new InvalidOperationException("Only requested refunds can be approved.");
        }

        if (approvedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Approved-by user id is required.", nameof(approvedByUserId));
        }

        Status = RefundStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovalReason = Required(approvalReason, nameof(approvalReason));
        ApprovedAtUtc = approvedAtUtc;
        UpdatedAtUtc = approvedAtUtc;
        ConcurrencyVersion++;
        AddEvent(RefundStatus.Approved, "RefundApproved", ApprovalReason, approvedAtUtc);
    }

    public void MarkProcessing(DateTimeOffset processingAtUtc)
    {
        if (Status != RefundStatus.Approved)
        {
            throw new InvalidOperationException("Only approved refunds can be processed.");
        }

        Status = RefundStatus.Processing;
        UpdatedAtUtc = processingAtUtc;
        ConcurrencyVersion++;
        AddEvent(RefundStatus.Processing, "RefundProcessing", "Refund provider call started.", processingAtUtc);
    }

    public void MarkRefunded(string providerRefundReference, DateTimeOffset refundedAtUtc)
    {
        if (Status != RefundStatus.Processing)
        {
            throw new InvalidOperationException("Only processing refunds can be marked refunded.");
        }

        Status = RefundStatus.Refunded;
        ProviderRefundReference = Required(providerRefundReference, nameof(providerRefundReference));
        RefundedAtUtc = refundedAtUtc;
        UpdatedAtUtc = refundedAtUtc;
        ConcurrencyVersion++;
        AddEvent(RefundStatus.Refunded, "Refunded", "Refund completed.", refundedAtUtc);
    }

    public void MarkProviderActionRequired(string message, DateTimeOffset updatedAtUtc)
    {
        if (Status != RefundStatus.Processing)
        {
            throw new InvalidOperationException("Only processing refunds can wait for provider action.");
        }

        UpdatedAtUtc = updatedAtUtc;
        ConcurrencyVersion++;
        AddEvent(RefundStatus.Processing, "ProviderRefundActionRequired", Required(message, nameof(message)), updatedAtUtc);
    }

    public void MarkFailed(string failureReason, DateTimeOffset failedAtUtc)
    {
        if (Status is RefundStatus.Refunded or RefundStatus.Rejected)
        {
            throw new InvalidOperationException("Completed refunds cannot be failed.");
        }

        Status = RefundStatus.Failed;
        FailureReason = Required(failureReason, nameof(failureReason));
        UpdatedAtUtc = failedAtUtc;
        ConcurrencyVersion++;
        AddEvent(RefundStatus.Failed, "RefundFailed", FailureReason, failedAtUtc);
    }

    private void AddEvent(RefundStatus status, string eventType, string message, DateTimeOffset createdAtUtc)
    {
        _events.Add(new RefundEvent(Id, status, eventType, message, createdAtUtc));
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
