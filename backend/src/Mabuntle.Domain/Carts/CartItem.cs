using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Carts;

public sealed class CartItem : AuditableEntity
{
    private CartItem()
    {
    }

    public CartItem(
        Guid cartId,
        Guid productId,
        Guid productVariantId,
        string? productTitle,
        string sku,
        string size,
        string colour,
        decimal unitPrice,
        int quantity)
    {
        if (cartId == Guid.Empty)
        {
            throw new ArgumentException("Cart id is required.", nameof(cartId));
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

        CartId = cartId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        ProductTitle = TrimOrNull(productTitle);
        Sku = Required(sku, nameof(sku));
        Size = Required(size, nameof(size));
        Colour = Required(colour, nameof(colour));
        UnitPrice = unitPrice;
        SetQuantity(quantity);
    }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public string? ProductTitle { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string Colour { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public void UpdateQuantity(int quantity, int availableQuantity)
    {
        if (quantity > availableQuantity)
        {
            throw new InvalidOperationException("Quantity cannot exceed available stock.");
        }

        SetQuantity(quantity);
    }

    public void RefreshVariantSnapshot(
        string sku,
        string size,
        string colour,
        decimal unitPrice)
    {
        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be positive.");
        }

        Sku = Required(sku, nameof(sku));
        Size = Required(size, nameof(size));
        Colour = Required(colour, nameof(colour));
        UnitPrice = unitPrice;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        Quantity = quantity;
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
