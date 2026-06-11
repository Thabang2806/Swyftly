using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class SellerPayoutAdjustment : Entity
{
    private SellerPayoutAdjustment()
    {
    }

    public SellerPayoutAdjustment(
        Guid sellerPayoutId,
        Guid? sellerPayoutItemId,
        Guid refundId,
        Guid refundLedgerEntryId,
        decimal amount,
        string currency,
        string adjustmentType,
        DateTimeOffset createdAtUtc,
        string? note = null)
    {
        if (sellerPayoutId == Guid.Empty)
        {
            throw new ArgumentException("Seller payout id is required.", nameof(sellerPayoutId));
        }

        if (sellerPayoutItemId == Guid.Empty)
        {
            throw new ArgumentException("Seller payout item id cannot be empty.", nameof(sellerPayoutItemId));
        }

        if (refundId == Guid.Empty)
        {
            throw new ArgumentException("Refund id is required.", nameof(refundId));
        }

        if (refundLedgerEntryId == Guid.Empty)
        {
            throw new ArgumentException("Refund ledger entry id is required.", nameof(refundLedgerEntryId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        SellerPayoutId = sellerPayoutId;
        SellerPayoutItemId = sellerPayoutItemId;
        RefundId = refundId;
        RefundLedgerEntryId = refundLedgerEntryId;
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        AdjustmentType = Required(adjustmentType, nameof(adjustmentType));
        CreatedAtUtc = createdAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid SellerPayoutId { get; private set; }

    public Guid? SellerPayoutItemId { get; private set; }

    public Guid RefundId { get; private set; }

    public Guid RefundLedgerEntryId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string AdjustmentType { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

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
