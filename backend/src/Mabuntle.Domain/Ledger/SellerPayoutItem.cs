using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class SellerPayoutItem : Entity
{
    private SellerPayoutItem()
    {
    }

    public SellerPayoutItem(
        Guid sellerPayoutId,
        Guid ledgerEntryId,
        Guid? orderId,
        Guid? paymentId,
        decimal amount,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        if (sellerPayoutId == Guid.Empty)
        {
            throw new ArgumentException("Seller payout id is required.", nameof(sellerPayoutId));
        }

        if (ledgerEntryId == Guid.Empty)
        {
            throw new ArgumentException("Ledger entry id is required.", nameof(ledgerEntryId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        SellerPayoutId = sellerPayoutId;
        LedgerEntryId = ledgerEntryId;
        OrderId = orderId == Guid.Empty ? null : orderId;
        PaymentId = paymentId == Guid.Empty ? null : paymentId;
        Amount = amount;
        AdjustedAmount = 0m;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SellerPayoutId { get; private set; }

    public Guid LedgerEntryId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public decimal Amount { get; private set; }

    public decimal AdjustedAmount { get; private set; }

    public decimal NetAmount => Amount - AdjustedAmount;

    public string Currency { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void ApplyAdjustment(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > NetAmount)
        {
            throw new InvalidOperationException("Adjustment cannot exceed the payout item net amount.");
        }

        AdjustedAmount += amount;
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
