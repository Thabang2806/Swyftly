using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class AiSuggestionFieldAudit : Entity
{
    private AiSuggestionFieldAudit()
    {
    }

    public AiSuggestionFieldAudit(
        Guid suggestionId,
        string fieldName,
        string? aiValue,
        string? sellerFinalValue,
        bool wasAccepted,
        bool wasEdited,
        DateTimeOffset createdAtUtc)
    {
        if (suggestionId == Guid.Empty)
        {
            throw new ArgumentException("Suggestion id is required.", nameof(suggestionId));
        }

        SuggestionId = suggestionId;
        FieldName = Required(fieldName, nameof(fieldName));
        AiValue = TrimOrNull(aiValue);
        SellerFinalValue = TrimOrNull(sellerFinalValue);
        WasAccepted = wasAccepted;
        WasEdited = wasEdited;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SuggestionId { get; private set; }

    public string FieldName { get; private set; } = string.Empty;

    public string? AiValue { get; private set; }

    public string? SellerFinalValue { get; private set; }

    public bool WasAccepted { get; private set; }

    public bool WasEdited { get; private set; }

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
}
