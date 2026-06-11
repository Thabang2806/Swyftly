using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class SellerAdCredit : AuditableEntity
{
    private SellerAdCredit()
    {
    }

    public SellerAdCredit(Guid sellerId, string currency, decimal balance, DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Ad credit balance cannot be negative.");
        }

        SellerId = sellerId;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Balance = balance;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public void Credit(decimal amount, DateTimeOffset updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");
        }

        Balance += amount;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Debit(decimal amount, DateTimeOffset updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
        }

        if (amount > Balance)
        {
            throw new InvalidOperationException("Ad credit balance is insufficient.");
        }

        Balance -= amount;
        UpdatedAtUtc = updatedAtUtc;
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
