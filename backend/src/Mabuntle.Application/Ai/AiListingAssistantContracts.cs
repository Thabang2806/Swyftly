using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Ai;

public interface IAiListingAssistantService
{
    Task<Result<AiListingSuggestionResponse>> GenerateSuggestionAsync(
        AiListingAssistantRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiProviderClient
{
    Task<AiProviderResponse> GenerateListingSuggestionAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiListingAssistantRequest(
    Guid SellerId,
    Guid ProductId,
    string? SellerNotes,
    string? ProductTypeHint,
    IReadOnlyDictionary<string, object?> KnownAttributes,
    Guid? CategoryHintId,
    IReadOnlyCollection<AiListingImageReference> ImageReferences,
    IReadOnlyCollection<AiListingCategoryReference> Categories,
    string? UserId = null,
    string PromptVersion = "listing-assistant-v1");

public sealed record AiListingImageReference(
    Guid ImageId,
    string? Url,
    string? AltText);

public sealed record AiListingCategoryReference(
    Guid CategoryId,
    string Name,
    string Slug,
    string Path,
    IReadOnlyCollection<AiListingCategoryAttributeReference> Attributes);

public sealed record AiListingCategoryAttributeReference(
    string Key,
    string Name,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string> AllowedValues);

public sealed record AiProviderRequest(
    string Prompt,
    string PromptVersion);

public sealed record AiProviderResponse(
    string Json,
    string ModelUsed,
    int? InputTokenEstimate,
    int? OutputTokenEstimate,
    decimal? CostEstimate,
    int LatencyMs);

public sealed record ValidatedAiSuggestion(
    string? SuggestedTitle,
    string? SuggestedShortDescription,
    string? SuggestedFullDescription,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryPath,
    string SuggestedAttributesJson,
    IReadOnlyDictionary<string, object?> SuggestedAttributes,
    string SuggestedTagsJson,
    IReadOnlyCollection<string> SuggestedTags,
    string MissingFieldsJson,
    IReadOnlyCollection<string> MissingFields,
    string RiskFlagsJson,
    IReadOnlyCollection<string> RiskFlags,
    decimal QualityScore);
