namespace Mabuntle.Application.Ai;

public sealed record AiListingSuggestionRequest(
    Guid ProductId,
    string? SellerNotes,
    IReadOnlyCollection<Guid> ImageIds,
    Guid? CategoryHintId);

public sealed record AiListingSuggestionResponse(
    Guid SuggestionId,
    Guid SellerId,
    Guid ProductId,
    string? SuggestedTitle,
    string? SuggestedShortDescription,
    string? SuggestedFullDescription,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryPath,
    IReadOnlyDictionary<string, object?> SuggestedAttributes,
    IReadOnlyCollection<string> SuggestedTags,
    IReadOnlyCollection<string> MissingFields,
    IReadOnlyCollection<string> RiskFlags,
    decimal QualityScore,
    string ModelUsed,
    string PromptVersion,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? AppliedAtUtc);
