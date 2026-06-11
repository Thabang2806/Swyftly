using System.Text.Json;
using Mabuntle.Domain.Ai;

namespace Mabuntle.Application.Catalog;

public sealed class ProductModerationService
{
    private static readonly string[] CounterfeitRiskTerms =
    [
        "replica",
        "aaa copy",
        "mirror quality",
        "designer inspired",
        "gucci style",
        "rolex style",
        "dupe"
    ];

    private static readonly string[] BeautyRiskClaims =
    [
        "cures acne",
        "removes scars permanently",
        "guaranteed skin whitening",
        "medical-grade treatment",
        "clinically proven",
        "permanent results"
    ];

    private static readonly string[] BeautyRequiredFields =
    [
        "ingredients",
        "expiry date",
        "batch number",
        "sealed/unsealed status"
    ];

    public ProductModerationDecision Moderate(ProductModerationRequest request)
    {
        var text = string.Join(
            " ",
            request.Title,
            request.ShortDescription,
            request.FullDescription,
            string.Join(" ", request.Tags),
            string.Join(" ", request.ImageAltTexts),
            JsonSerializer.Serialize(request.Attributes));
        var detectedCounterfeitTerms = FindTerms(text, CounterfeitRiskTerms);
        var detectedBeautyClaims = FindTerms(text, BeautyRiskClaims);
        var missingBeautyFields = IsBeautyCategory(request.CategoryPath)
            ? FindMissingBeautyFields(request.Attributes, text)
            : [];

        var flags = new List<ProductModerationFlag>();
        if (detectedCounterfeitTerms.Count > 0)
        {
            flags.Add(new ProductModerationFlag(
                "CounterfeitRisk",
                AiModerationRiskLevel.High,
                "Listing text contains counterfeit-risk wording and requires admin review.",
                detectedCounterfeitTerms));
        }

        if (detectedBeautyClaims.Count > 0)
        {
            flags.Add(new ProductModerationFlag(
                "BeautyClaimRisk",
                AiModerationRiskLevel.High,
                "Beauty listing text contains claims that require admin review.",
                detectedBeautyClaims));
        }

        if (missingBeautyFields.Count > 0)
        {
            flags.Add(new ProductModerationFlag(
                "BeautyMissingFields",
                AiModerationRiskLevel.High,
                "Beauty listing is missing safety and product detail fields.",
                missingBeautyFields));
        }

        var riskLevel = flags.Count == 0
            ? AiModerationRiskLevel.Low
            : flags.Max(flag => flag.RiskLevel);
        var needsAdminReview = riskLevel == AiModerationRiskLevel.High;
        var detectedTerms = detectedCounterfeitTerms
            .Concat(detectedBeautyClaims)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reason = flags.Count == 0
            ? "No moderation risk flags detected."
            : "Moderation flags require admin review.";

        return new ProductModerationDecision(
            riskLevel,
            needsAdminReview,
            reason,
            detectedTerms,
            missingBeautyFields,
            flags,
            Provider: "business-rules-placeholder");
    }

    private static IReadOnlyCollection<string> FindTerms(string text, IEnumerable<string> terms)
    {
        return terms
            .Where(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyCollection<string> FindMissingBeautyFields(
        IReadOnlyDictionary<string, object?> attributes,
        string text)
    {
        return BeautyRequiredFields
            .Where(field => !HasField(attributes, field, text))
            .ToArray();
    }

    private static bool HasField(
        IReadOnlyDictionary<string, object?> attributes,
        string field,
        string text)
    {
        var normalizedField = NormalizeKey(field);
        if (attributes.Any(attribute =>
            NormalizeKey(attribute.Key) == normalizedField &&
            !IsMissing(attribute.Value)))
        {
            return true;
        }

        return text.Contains(field, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBeautyCategory(string? categoryPath) =>
        categoryPath?.Contains("Beauty", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeKey(string value) =>
        value.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static bool IsMissing(object? value)
    {
        return value is null
            || value is string text && string.IsNullOrWhiteSpace(text)
            || value is Array array && array.Length == 0;
    }
}

public sealed record ProductModerationRequest(
    Guid ProductId,
    Guid SellerId,
    string? CategoryPath,
    string? Title,
    string? ShortDescription,
    string? FullDescription,
    IReadOnlyDictionary<string, object?> Attributes,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> ImageAltTexts);

public sealed record ProductModerationDecision(
    AiModerationRiskLevel RiskLevel,
    bool NeedsAdminReview,
    string Reason,
    IReadOnlyCollection<string> DetectedTerms,
    IReadOnlyCollection<string> MissingFields,
    IReadOnlyCollection<ProductModerationFlag> Flags,
    string Provider);

public sealed record ProductModerationFlag(
    string Code,
    AiModerationRiskLevel RiskLevel,
    string Message,
    IReadOnlyCollection<string> Terms);
