using System.Text.Json;
using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class CategoryAttribute : Entity
{
    private CategoryAttribute()
    {
    }

    public CategoryAttribute(
        Guid categoryId,
        string name,
        string key,
        CategoryAttributeDataType dataType,
        bool isRequired,
        IEnumerable<string>? allowedValues = null,
        int displayOrder = 0,
        bool isActive = true)
        : this(Guid.NewGuid(), categoryId, name, key, dataType, isRequired, allowedValues, displayOrder, isActive)
    {
    }

    public CategoryAttribute(
        Guid id,
        Guid categoryId,
        string name,
        string key,
        CategoryAttributeDataType dataType,
        bool isRequired,
        IEnumerable<string>? allowedValues = null,
        int displayOrder = 0,
        bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Category attribute id is required.", nameof(id));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category id is required.", nameof(categoryId));
        }

        Id = id;
        CategoryId = categoryId;
        Name = Required(name, nameof(name));
        Key = NormalizeKey(key);
        DataType = dataType;
        IsRequired = isRequired;
        AllowedValuesJson = SerializeAllowedValues(dataType, allowedValues);
        DisplayOrder = NonNegative(displayOrder, nameof(displayOrder));
        IsActive = isActive;
    }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public CategoryAttributeDataType DataType { get; private set; }

    public bool IsRequired { get; private set; }

    public string AllowedValuesJson { get; private set; } = "[]";

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<string> AllowedValues =>
        JsonSerializer.Deserialize<IReadOnlyCollection<string>>(AllowedValuesJson) ?? [];

    public bool HasAllowedValue(string value) =>
        AllowedValues.Any(allowedValue => string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase));

    public void Update(
        string name,
        string key,
        CategoryAttributeDataType dataType,
        bool isRequired,
        IEnumerable<string>? allowedValues,
        int displayOrder)
    {
        Name = Required(name, nameof(name));
        Key = NormalizeKey(key);
        DataType = dataType;
        IsRequired = isRequired;
        AllowedValuesJson = SerializeAllowedValues(dataType, allowedValues);
        DisplayOrder = NonNegative(displayOrder, nameof(displayOrder));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static int NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
        }

        return value;
    }

    private static string NormalizeKey(string key)
    {
        var normalized = Required(key, nameof(key)).Trim().ToLowerInvariant();
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Attribute key can only contain letters, numbers, and hyphens.", nameof(key));
        }

        return normalized;
    }

    private static string SerializeAllowedValues(
        CategoryAttributeDataType dataType,
        IEnumerable<string>? allowedValues)
    {
        var values = (allowedValues ?? [])
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dataType is CategoryAttributeDataType.Select or CategoryAttributeDataType.MultiSelect)
        {
            if (values.Length == 0)
            {
                throw new ArgumentException("Select attributes require allowed values.", nameof(allowedValues));
            }
        }
        else if (values.Length > 0)
        {
            throw new ArgumentException("Allowed values are only valid for Select and MultiSelect attributes.", nameof(allowedValues));
        }

        return JsonSerializer.Serialize(values);
    }
}
