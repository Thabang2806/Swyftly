using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class AiProductSuggestion : Entity
{
    private AiProductSuggestion()
    {
    }

    public AiProductSuggestion(
        Guid sellerId,
        Guid productId,
        string? inputNotes,
        string inputImageIdsJson,
        string? suggestedTitle,
        string? suggestedShortDescription,
        string? suggestedFullDescription,
        Guid? suggestedCategoryId,
        string? suggestedCategoryPath,
        string suggestedAttributesJson,
        string suggestedTagsJson,
        string missingFieldsJson,
        string riskFlagsJson,
        decimal qualityScore,
        string modelUsed,
        string promptVersion,
        DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (suggestedCategoryId == Guid.Empty)
        {
            throw new ArgumentException("Suggested category id cannot be empty.", nameof(suggestedCategoryId));
        }

        if (qualityScore < 0 || qualityScore > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(qualityScore), "Quality score must be between 0 and 100.");
        }

        SellerId = sellerId;
        ProductId = productId;
        InputNotes = TrimOrNull(inputNotes);
        InputImageIdsJson = Required(inputImageIdsJson, nameof(inputImageIdsJson));
        SuggestedTitle = TrimOrNull(suggestedTitle);
        SuggestedShortDescription = TrimOrNull(suggestedShortDescription);
        SuggestedFullDescription = TrimOrNull(suggestedFullDescription);
        SuggestedCategoryId = suggestedCategoryId;
        SuggestedCategoryPath = TrimOrNull(suggestedCategoryPath);
        SuggestedAttributesJson = Required(suggestedAttributesJson, nameof(suggestedAttributesJson));
        SuggestedTagsJson = Required(suggestedTagsJson, nameof(suggestedTagsJson));
        MissingFieldsJson = Required(missingFieldsJson, nameof(missingFieldsJson));
        RiskFlagsJson = Required(riskFlagsJson, nameof(riskFlagsJson));
        QualityScore = qualityScore;
        ModelUsed = Required(modelUsed, nameof(modelUsed));
        PromptVersion = Required(promptVersion, nameof(promptVersion));
        Status = AiProductSuggestionStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public Guid ProductId { get; private set; }

    public string? InputNotes { get; private set; }

    public string InputImageIdsJson { get; private set; } = "[]";

    public string? SuggestedTitle { get; private set; }

    public string? SuggestedShortDescription { get; private set; }

    public string? SuggestedFullDescription { get; private set; }

    public Guid? SuggestedCategoryId { get; private set; }

    public string? SuggestedCategoryPath { get; private set; }

    public string SuggestedAttributesJson { get; private set; } = "{}";

    public string SuggestedTagsJson { get; private set; } = "[]";

    public string MissingFieldsJson { get; private set; } = "[]";

    public string RiskFlagsJson { get; private set; } = "[]";

    public decimal QualityScore { get; private set; }

    public string ModelUsed { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public AiProductSuggestionStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public DateTimeOffset? AppliedAtUtc { get; private set; }

    public void Accept(DateTimeOffset acceptedAtUtc)
    {
        if (Status != AiProductSuggestionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft AI suggestions can be accepted.");
        }

        Status = AiProductSuggestionStatus.Accepted;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public void Reject()
    {
        if (Status != AiProductSuggestionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft AI suggestions can be rejected.");
        }

        Status = AiProductSuggestionStatus.Rejected;
    }

    public void MarkApplied(DateTimeOffset appliedAtUtc)
    {
        if (Status != AiProductSuggestionStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted AI suggestions can be applied.");
        }

        Status = AiProductSuggestionStatus.Applied;
        AppliedAtUtc = appliedAtUtc;
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

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
