using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class SellerBalance : AuditableEntity
{
    private SellerBalance()
    {
    }

    public SellerBalance(Guid sellerId, string currency)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
    }

    public Guid SellerId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal PendingBalance { get; private set; }

    public decimal AvailableBalance { get; private set; }

    public decimal HeldBalance { get; private set; }

    public void CreditPending(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        PendingBalance += amount;
    }

    public void HoldPending(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > PendingBalance)
        {
            throw new InvalidOperationException("Cannot hold more than the pending balance.");
        }

        PendingBalance -= amount;
        HeldBalance += amount;
    }

    public void MovePendingToAvailable(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > PendingBalance)
        {
            throw new InvalidOperationException("Cannot make more than the pending balance available.");
        }

        PendingBalance -= amount;
        AvailableBalance += amount;
    }

    public void HoldAvailable(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > AvailableBalance)
        {
            throw new InvalidOperationException("Cannot hold more than the available balance.");
        }

        AvailableBalance -= amount;
        HeldBalance += amount;
    }

    public void ReleaseHeldToPending(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > HeldBalance)
        {
            throw new InvalidOperationException("Cannot release more than the held balance.");
        }

        HeldBalance -= amount;
        PendingBalance += amount;
    }

    public void ReleaseHeldToAvailable(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > HeldBalance)
        {
            throw new InvalidOperationException("Cannot release more than the held balance.");
        }

        HeldBalance -= amount;
        AvailableBalance += amount;
    }

    public void DebitPending(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > PendingBalance)
        {
            throw new InvalidOperationException("Cannot debit more than the pending balance.");
        }

        PendingBalance -= amount;
    }

    public void DebitAvailable(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > AvailableBalance)
        {
            throw new InvalidOperationException("Cannot debit more than the available balance.");
        }

        AvailableBalance -= amount;
    }

    public void CreditAvailable(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        AvailableBalance += amount;
    }

    public void DebitHeld(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > HeldBalance)
        {
            throw new InvalidOperationException("Cannot debit more than the held balance.");
        }

        HeldBalance -= amount;
    }

    public void ApplyRefundDebit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        var heldDebit = Math.Min(HeldBalance, amount);
        HeldBalance -= heldDebit;
        var remaining = amount - heldDebit;

        var pendingDebit = Math.Min(PendingBalance, remaining);
        PendingBalance -= pendingDebit;
        remaining -= pendingDebit;

        var availableDebit = Math.Min(AvailableBalance, remaining);
        AvailableBalance -= availableDebit;
        remaining -= availableDebit;

        if (remaining > 0)
        {
            PendingBalance -= remaining;
        }
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
