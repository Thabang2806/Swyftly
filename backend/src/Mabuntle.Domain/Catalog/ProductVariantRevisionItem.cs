using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class ProductVariantRevisionItem : Entity
{
    private ProductVariantRevisionItem()
    {
    }

    public ProductVariantRevisionItem(
        Guid revisionId,
        ProductVariantRevisionItemOperation operation,
        Guid? sourceVariantId,
        string sku,
        string size,
        string colour,
        decimal price,
        decimal? compareAtPrice,
        int? initialStockQuantity,
        ProductVariantStatus proposedStatus,
        string? barcode)
    {
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("Revision id is required.", nameof(revisionId));
        }

        ValidateOperation(operation, sourceVariantId, initialStockQuantity);
        ValidatePrice(price, compareAtPrice);

        RevisionId = revisionId;
        Operation = operation;
        SourceVariantId = sourceVariantId;
        Sku = Required(sku, nameof(sku));
        Size = Required(size, nameof(size));
        Colour = Required(colour, nameof(colour));
        Price = price;
        CompareAtPrice = compareAtPrice;
        InitialStockQuantity = initialStockQuantity;
        ProposedStatus = operation == ProductVariantRevisionItemOperation.Deactivate
            ? ProductVariantStatus.Inactive
            : proposedStatus;
        Barcode = TrimOrNull(barcode);
    }

    public Guid RevisionId { get; private set; }

    public ProductVariantRevisionItemOperation Operation { get; private set; }

    public Guid? SourceVariantId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string Colour { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    public int? InitialStockQuantity { get; private set; }

    public ProductVariantStatus ProposedStatus { get; private set; }

    public string? Barcode { get; private set; }

    private static void ValidateOperation(
        ProductVariantRevisionItemOperation operation,
        Guid? sourceVariantId,
        int? initialStockQuantity)
    {
        if (operation == ProductVariantRevisionItemOperation.Add)
        {
            if (sourceVariantId.HasValue)
            {
                throw new ArgumentException("New variant items cannot reference an existing variant.", nameof(sourceVariantId));
            }

            if (!initialStockQuantity.HasValue || initialStockQuantity.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialStockQuantity), "New variant initial stock must be zero or greater.");
            }

            return;
        }

        if (!sourceVariantId.HasValue || sourceVariantId.Value == Guid.Empty)
        {
            throw new ArgumentException("Existing variant changes require a source variant id.", nameof(sourceVariantId));
        }

        if (initialStockQuantity.HasValue)
        {
            throw new ArgumentException("Existing variant stock cannot be changed through a variant revision.", nameof(initialStockQuantity));
        }
    }

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
