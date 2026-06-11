using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class LedgerEntry : Entity
{
    private LedgerEntry()
    {
    }

    public LedgerEntry(
        Guid? orderId,
        Guid? orderItemId,
        Guid? sellerId,
        Guid? buyerId,
        Guid? paymentId,
        LedgerEntryType type,
        decimal amount,
        string currency,
        LedgerDirection direction,
        string description,
        DateTimeOffset createdAtUtc)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        OrderId = EmptyToNull(orderId);
        OrderItemId = EmptyToNull(orderItemId);
        SellerId = EmptyToNull(sellerId);
        BuyerId = EmptyToNull(buyerId);
        PaymentId = EmptyToNull(paymentId);
        Type = type;
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Direction = direction;
        Description = Required(description, nameof(description));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid? OrderId { get; private set; }

    public Guid? OrderItemId { get; private set; }

    public Guid? SellerId { get; private set; }

    public Guid? BuyerId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public LedgerEntryType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public LedgerDirection Direction { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static Guid? EmptyToNull(Guid? value) => value == Guid.Empty ? null : value;

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
