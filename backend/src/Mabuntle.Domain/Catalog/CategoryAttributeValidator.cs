using System.Collections;
using System.Globalization;

namespace Mabuntle.Domain.Catalog;

public static class CategoryAttributeValidator
{
    public static CategoryAttributeValidationResult Validate(
        Guid categoryId,
        IEnumerable<CategoryAttribute> attributeDefinitions,
        IReadOnlyDictionary<string, object?> productAttributes)
    {
        var definitions = attributeDefinitions
            .Where(definition => definition.CategoryId == categoryId && definition.IsActive)
            .ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();

        foreach (var key in productAttributes.Keys)
        {
            if (!definitions.ContainsKey(key))
            {
                errors.Add($"Attribute '{key}' is not valid for the selected category.");
            }
        }

        foreach (var definition in definitions.Values)
        {
            productAttributes.TryGetValue(definition.Key, out var value);

            if (IsMissing(value))
            {
                if (definition.IsRequired)
                {
                    errors.Add($"Attribute '{definition.Key}' is required.");
                }

                continue;
            }

            if (value is not null && !HasValidValue(definition, value))
            {
                errors.Add($"Attribute '{definition.Key}' has an invalid value.");
            }
        }

        return errors.Count == 0
            ? CategoryAttributeValidationResult.Success
            : new CategoryAttributeValidationResult(errors);
    }

    private static bool IsMissing(object? value)
    {
        return value is null
            || value is string stringValue && string.IsNullOrWhiteSpace(stringValue)
            || value is IEnumerable enumerable && value is not string && !enumerable.Cast<object?>().Any();
    }

    private static bool HasValidValue(CategoryAttribute definition, object value)
    {
        return definition.DataType switch
        {
            CategoryAttributeDataType.Text => value is string text && !string.IsNullOrWhiteSpace(text),
            CategoryAttributeDataType.Number => IsInteger(value),
            CategoryAttributeDataType.Decimal => IsDecimal(value),
            CategoryAttributeDataType.Boolean => value is bool || value is string text && bool.TryParse(text, out _),
            CategoryAttributeDataType.Date => IsDate(value),
            CategoryAttributeDataType.Select => value is string selected && definition.HasAllowedValue(selected),
            CategoryAttributeDataType.MultiSelect => IsValidMultiSelect(definition, value),
            _ => false
        };
    }

    private static bool IsInteger(object value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong
            || value is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsDecimal(object value)
    {
        return value is decimal or double or float or sbyte or byte or short or ushort or int or uint or long or ulong
            || value is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsDate(object value)
    {
        return value is DateOnly or DateTime or DateTimeOffset
            || value is string text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool IsValidMultiSelect(CategoryAttribute definition, object value)
    {
        if (value is string)
        {
            return false;
        }

        if (value is not IEnumerable enumerable)
        {
            return false;
        }

        var values = enumerable
            .Cast<object?>()
            .Select(item => item?.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return values.Length > 0 && values.All(value => definition.HasAllowedValue(value!));
    }
}
