using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class AiUsageLog : Entity
{
    private AiUsageLog()
    {
    }

    public AiUsageLog(
        string featureName,
        string userId,
        Guid? sellerId,
        string modelUsed,
        int? inputTokenEstimate,
        int? outputTokenEstimate,
        decimal? costEstimate,
        int latencyMs,
        bool success,
        string? errorMessage,
        DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id cannot be empty.", nameof(sellerId));
        }

        ValidateNonNegative(inputTokenEstimate, nameof(inputTokenEstimate));
        ValidateNonNegative(outputTokenEstimate, nameof(outputTokenEstimate));

        if (costEstimate is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costEstimate), "Cost estimate cannot be negative.");
        }

        if (latencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(latencyMs), "Latency cannot be negative.");
        }

        FeatureName = Required(featureName, nameof(featureName));
        UserId = Required(userId, nameof(userId));
        SellerId = sellerId;
        ModelUsed = Required(modelUsed, nameof(modelUsed));
        InputTokenEstimate = inputTokenEstimate;
        OutputTokenEstimate = outputTokenEstimate;
        CostEstimate = costEstimate;
        LatencyMs = latencyMs;
        Success = success;
        ErrorMessage = TrimOrNull(errorMessage);
        CreatedAtUtc = createdAtUtc;
    }

    public string FeatureName { get; private set; } = string.Empty;

    public string UserId { get; private set; } = string.Empty;

    public Guid? SellerId { get; private set; }

    public string ModelUsed { get; private set; } = string.Empty;

    public int? InputTokenEstimate { get; private set; }

    public int? OutputTokenEstimate { get; private set; }

    public decimal? CostEstimate { get; private set; }

    public int LatencyMs { get; private set; }

    public bool Success { get; private set; }

    public string? ErrorMessage { get; private set; }

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

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static void ValidateNonNegative(int? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
        }
    }
}
