using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdBudget : AuditableEntity
{
    private AdBudget()
    {
    }

    public AdBudget(
        Guid adCampaignId,
        string currency,
        decimal dailyBudget,
        decimal totalBudget,
        decimal maxCostPerClick,
        DateTimeOffset createdAtUtc)
    {
        if (adCampaignId == Guid.Empty)
        {
            throw new ArgumentException("Ad campaign id is required.", nameof(adCampaignId));
        }

        AdCampaignId = adCampaignId;
        Update(currency, dailyBudget, totalBudget, maxCostPerClick, createdAtUtc);
        SpentAmount = 0;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal DailyBudget { get; private set; }

    public decimal TotalBudget { get; private set; }

    public decimal MaxCostPerClick { get; private set; }

    public decimal SpentAmount { get; private set; }

    public void Update(string currency, decimal dailyBudget, decimal totalBudget, decimal maxCostPerClick, DateTimeOffset updatedAtUtc)
    {
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();

        if (dailyBudget <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyBudget), "Daily budget must be positive.");
        }

        if (totalBudget <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBudget), "Total budget must be positive.");
        }

        if (dailyBudget > totalBudget)
        {
            throw new ArgumentException("Daily budget cannot exceed total budget.", nameof(dailyBudget));
        }

        if (maxCostPerClick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCostPerClick), "Max cost per click must be positive.");
        }

        DailyBudget = dailyBudget;
        TotalBudget = totalBudget;
        MaxCostPerClick = maxCostPerClick;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void AddSpend(decimal amount, DateTimeOffset updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Spend amount must be positive.");
        }

        if (SpentAmount + amount > TotalBudget)
        {
            throw new InvalidOperationException("Ad spend cannot exceed total budget.");
        }

        SpentAmount += amount;
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
