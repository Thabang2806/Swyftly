using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Payments;

public sealed class Payment : AuditableEntity
{
    private Payment()
    {
    }

    public Payment(
        Guid orderId,
        Guid buyerId,
        string provider,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        OrderId = orderId;
        BuyerId = buyerId;
        Provider = Required(provider, nameof(provider));
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Status = PaymentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid OrderId { get; private set; }

    public Guid BuyerId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderReference { get; private set; }

    public string? CheckoutUrl { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public void SetProviderReference(string providerReference, DateTimeOffset updatedAtUtc)
    {
        ProviderReference = Required(providerReference, nameof(providerReference));
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetCheckoutUrl(Uri? checkoutUrl, DateTimeOffset updatedAtUtc)
    {
        CheckoutUrl = checkoutUrl?.ToString();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkPaid(DateTimeOffset paidAtUtc)
    {
        if (Status == PaymentStatus.Paid)
        {
            return;
        }

        if (Status is PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.Disputed)
        {
            throw new InvalidOperationException("Payment cannot be marked paid from its current status.");
        }

        Status = PaymentStatus.Paid;
        PaidAtUtc = paidAtUtc;
        UpdatedAtUtc = paidAtUtc;
    }

    public void MarkAuthorized(DateTimeOffset authorizedAtUtc)
    {
        if (Status == PaymentStatus.Authorized)
        {
            return;
        }

        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payments can be marked authorized.");
        }

        Status = PaymentStatus.Authorized;
        UpdatedAtUtc = authorizedAtUtc;
    }

    public void MarkFailed(DateTimeOffset failedAtUtc)
    {
        if (Status == PaymentStatus.Failed)
        {
            return;
        }

        if (Status is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.Disputed)
        {
            throw new InvalidOperationException("Payment cannot be marked failed from its current status.");
        }

        Status = PaymentStatus.Failed;
        FailedAtUtc = failedAtUtc;
        UpdatedAtUtc = failedAtUtc;
    }

    public void MarkCancelled(DateTimeOffset cancelledAtUtc)
    {
        if (Status == PaymentStatus.Cancelled)
        {
            return;
        }

        if (Status is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.Disputed)
        {
            throw new InvalidOperationException("Payment cannot be cancelled from its current status.");
        }

        Status = PaymentStatus.Cancelled;
        UpdatedAtUtc = cancelledAtUtc;
    }

    public void ApplyRefund(decimal totalRefundedAmount, DateTimeOffset refundedAtUtc)
    {
        if (Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException("Only paid payments can be refunded.");
        }

        if (totalRefundedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRefundedAmount), "Refunded amount must be positive.");
        }

        if (totalRefundedAmount > Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRefundedAmount), "Refunded amount cannot exceed the payment amount.");
        }

        Status = totalRefundedAmount == Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;
        UpdatedAtUtc = refundedAtUtc;
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
