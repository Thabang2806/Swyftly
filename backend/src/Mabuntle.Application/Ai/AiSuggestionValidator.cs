using System.Text.Json;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;

namespace Mabuntle.Application.Ai;

public sealed class AiSuggestionValidator
{
    public Result<ValidatedAiSuggestion> Validate(
        string providerJson,
        AiListingAssistantRequest request)
    {
        using JsonDocument document = TryParse(providerJson, out var parseError);
        if (parseError is not null)
        {
            return ValidationFailure("json", parseError);
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ValidationFailure("json", "AI response must be a JSON object.");
        }

        var errors = new List<ValidationFailure>();
        var suggestedCategoryId = ReadOptionalGuid(root, "suggestedCategoryId", errors);
        var categoriesById = request.Categories.ToDictionary(category => category.CategoryId);

        if (suggestedCategoryId.HasValue && !categoriesById.ContainsKey(suggestedCategoryId.Value))
        {
            errors.Add(new ValidationFailure("suggestedCategoryId", "Suggested category must exist in the marketplace category list."));
        }

        var suggestedAttributes = ReadObject(root, "suggestedAttributes", errors);
        if (suggestedCategoryId.HasValue && categoriesById.TryGetValue(suggestedCategoryId.Value, out var category))
        {
            var attributesByKey = category.Attributes
                .ToDictionary(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var key in suggestedAttributes.Keys)
            {
                if (!attributesByKey.TryGetValue(key, out var attribute))
                {
                    errors.Add(new ValidationFailure("suggestedAttributes", $"Attribute '{key}' is not allowed for the suggested category."));
                    continue;
                }

                var allowedValues = attribute.AllowedValues
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (allowedValues.Count > 0 && !UsesOnlyAllowedValues(suggestedAttributes[key], allowedValues))
                {
                    errors.Add(new ValidationFailure("suggestedAttributes", $"Attribute '{key}' contains a value that is not allowed for the suggested category."));
                }
            }
        }

        var qualityScore = ReadQualityScore(root, errors);
        var tags = ReadStringArray(root, "suggestedTags", errors);
        var missingFields = ReadStringArray(root, "missingFields", errors);
        var riskFlags = ReadStringArray(root, "riskFlags", errors);

        if (errors.Count > 0)
        {
            return Result<ValidatedAiSuggestion>.Failure(Error.Validation(errors));
        }

        var validated = new ValidatedAiSuggestion(
            ReadOptionalString(root, "suggestedTitle"),
            ReadOptionalString(root, "suggestedShortDescription"),
            ReadOptionalString(root, "suggestedFullDescription"),
            suggestedCategoryId,
            ReadOptionalString(root, "suggestedCategoryPath"),
            JsonSerializer.Serialize(suggestedAttributes),
            suggestedAttributes,
            JsonSerializer.Serialize(tags),
            tags,
            JsonSerializer.Serialize(missingFields),
            missingFields,
            JsonSerializer.Serialize(riskFlags),
            riskFlags,
            qualityScore);

        return Result<ValidatedAiSuggestion>.Success(validated);
    }

    private static JsonDocument TryParse(string providerJson, out string? parseError)
    {
        parseError = null;

        try
        {
            return JsonDocument.Parse(providerJson);
        }
        catch (JsonException exception)
        {
            parseError = $"AI response was not valid JSON: {exception.Message}";
            return JsonDocument.Parse("{}");
        }
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.GetRawText();
    }

    private static Guid? ReadOptionalGuid(
        JsonElement root,
        string propertyName,
        List<ValidationFailure> errors)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String
            && Guid.TryParse(element.GetString(), out var id)
            && id != Guid.Empty)
        {
            return id;
        }

        errors.Add(new ValidationFailure(propertyName, $"{propertyName} must be a non-empty GUID or null."));
        return null;
    }

    private static Dictionary<string, object?> ReadObject(
        JsonElement root,
        string propertyName,
        List<ValidationFailure> errors)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ValidationFailure(propertyName, $"{propertyName} must be a JSON object."));
            return [];
        }

        return element.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ToObject(property.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> ReadStringArray(
        JsonElement root,
        string propertyName,
        List<ValidationFailure> errors)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationFailure(propertyName, $"{propertyName} must be an array of strings."));
            return [];
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add(new ValidationFailure(propertyName, $"{propertyName} must contain only strings."));
                continue;
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static decimal ReadQualityScore(JsonElement root, List<ValidationFailure> errors)
    {
        if (!root.TryGetProperty("qualityScore", out var element) || element.ValueKind != JsonValueKind.Number)
        {
            errors.Add(new ValidationFailure("qualityScore", "qualityScore must be a number between 0 and 100."));
            return 0;
        }

        var score = element.GetDecimal();
        if (score < 0 || score > 100)
        {
            errors.Add(new ValidationFailure("qualityScore", "qualityScore must be between 0 and 100."));
        }

        return score;
    }

    private static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ToObject(property.Value)),
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static bool UsesOnlyAllowedValues(object? value, HashSet<string> allowedValues)
    {
        return value switch
        {
            null => true,
            string text => allowedValues.Contains(text),
            object?[] values => values.All(item => UsesOnlyAllowedValues(item, allowedValues)),
            _ => false
        };
    }

    private static Result<ValidatedAiSuggestion> ValidationFailure(string propertyName, string message)
    {
        return Result<ValidatedAiSuggestion>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));
    }
}
