using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class AiModerationResult : Entity
{
    private AiModerationResult()
    {
    }

    public AiModerationResult(
        Guid productId,
        Guid sellerId,
        AiModerationRiskLevel riskLevel,
        bool needsAdminReview,
        string reason,
        string detectedTermsJson,
        string missingFieldsJson,
        string flagsJson,
        string provider,
        DateTimeOffset createdAtUtc)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        ProductId = productId;
        SellerId = sellerId;
        RiskLevel = riskLevel;
        NeedsAdminReview = needsAdminReview;
        Reason = Required(reason, nameof(reason));
        DetectedTermsJson = Required(detectedTermsJson, nameof(detectedTermsJson));
        MissingFieldsJson = Required(missingFieldsJson, nameof(missingFieldsJson));
        FlagsJson = Required(flagsJson, nameof(flagsJson));
        Provider = Required(provider, nameof(provider));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid ProductId { get; private set; }

    public Guid SellerId { get; private set; }

    public AiModerationRiskLevel RiskLevel { get; private set; }

    public bool NeedsAdminReview { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string DetectedTermsJson { get; private set; } = "[]";

    public string MissingFieldsJson { get; private set; } = "[]";

    public string FlagsJson { get; private set; } = "[]";

    public string Provider { get; private set; } = string.Empty;

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
