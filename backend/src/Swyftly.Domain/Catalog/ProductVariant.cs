using Swyftly.Domain.Common;

namespace Swyftly.Domain.Catalog;

public sealed class ProductVariant : AuditableEntity
{
    private ProductVariant()
    {
    }

    public ProductVariant(
        Guid productId,
        string sku,
        string size,
        string colour,
        decimal price,
        decimal? compareAtPrice,
        int stockQuantity,
        int reservedQuantity = 0,
        ProductVariantStatus status = ProductVariantStatus.Active,
        string? barcode = null)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        ProductId = productId;
        Update(sku, size, colour, price, compareAtPrice, stockQuantity, reservedQuantity, status, barcode);
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string Colour { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    public int StockQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public ProductVariantStatus Status { get; private set; }

    public string? Barcode { get; private set; }

    public int AvailableQuantity => StockQuantity - ReservedQuantity;

    public bool HasSellableStock => Status == ProductVariantStatus.Active && AvailableQuantity > 0;

    public void Update(
        string sku,
        string size,
        string colour,
        decimal price,
        decimal? compareAtPrice,
        int stockQuantity,
        int reservedQuantity,
        ProductVariantStatus status,
        string? barcode)
    {
        ValidatePrice(price, compareAtPrice);
        ValidateStock(stockQuantity, reservedQuantity);

        Sku = Required(sku, nameof(sku));
        Size = Required(size, nameof(size));
        Colour = Required(colour, nameof(colour));
        Price = price;
        CompareAtPrice = compareAtPrice;
        StockQuantity = stockQuantity;
        ReservedQuantity = reservedQuantity;
        Status = status;
        Barcode = TrimOrNull(barcode);
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ValidateStock(StockQuantity, ReservedQuantity + quantity);
        ReservedQuantity += quantity;
    }

    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (quantity > ReservedQuantity)
        {
            throw new InvalidOperationException("Cannot release more stock than is reserved.");
        }

        ReservedQuantity -= quantity;
    }

    public void AdjustInventory(int stockQuantity, ProductVariantStatus status)
    {
        ValidateStock(stockQuantity, ReservedQuantity);

        StockQuantity = stockQuantity;
        Status = status;
    }

    public void Activate() => Status = ProductVariantStatus.Active;

    public void Deactivate() => Status = ProductVariantStatus.Inactive;

    public void MarkOutOfStock() => Status = ProductVariantStatus.OutOfStock;

    private static void ValidatePrice(decimal price, decimal? compareAtPrice)
    {
        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");
        }

        if (compareAtPrice is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compareAtPrice), "Compare-at price must be positive.");
        }

        if (compareAtPrice.HasValue && compareAtPrice <= price)
        {
            throw new ArgumentException("Compare-at price must be greater than price.", nameof(compareAtPrice));
        }
    }

    private static void ValidateStock(int stockQuantity, int reservedQuantity)
    {
        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock quantity cannot be negative.");
        }

        if (reservedQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedQuantity), "Reserved quantity cannot be negative.");
        }

        if (reservedQuantity > stockQuantity)
        {
            throw new InvalidOperationException("Reserved quantity cannot exceed stock quantity.");
        }
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
