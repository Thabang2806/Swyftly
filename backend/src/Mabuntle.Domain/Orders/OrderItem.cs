using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Orders;

public sealed class OrderItem : Entity
{
    private OrderItem()
    {
    }

    public OrderItem(
        Guid orderId,
        Guid productId,
        Guid productVariantId,
        string? productTitle,
        string sku,
        string size,
        string colour,
        decimal unitPrice,
        int quantity)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (productVariantId == Guid.Empty)
        {
            throw new ArgumentException("Product variant id is required.", nameof(productVariantId));
        }

        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be positive.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        OrderId = orderId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        ProductTitle = TrimOrNull(productTitle);
        Sku = Required(sku, nameof(sku));
        Size = Required(size, nameof(size));
        Colour = Required(colour, nameof(colour));
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public string? ProductTitle { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string Colour { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

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
