using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class ProductAttributeValue : Entity
{
    private ProductAttributeValue()
    {
    }

    public ProductAttributeValue(Guid productId, string key, string valueJson)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        ProductId = productId;
        Key = Required(key, nameof(key)).ToLowerInvariant();
        ValueJson = Required(valueJson, nameof(valueJson));
    }

    public Guid ProductId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string ValueJson { get; private set; } = string.Empty;

    public void UpdateValue(string valueJson)
    {
        ValueJson = Required(valueJson, nameof(valueJson));
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
}
