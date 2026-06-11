using Microsoft.EntityFrameworkCore;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Api.Catalog;

internal static class ProductVariantRevisionRules
{
    public static async Task<ProductVariantRevisionValidationResult> ValidateAsync(
        Guid productId,
        IReadOnlyCollection<ProductVariantRevisionItem> items,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (items.Count == 0)
        {
            AddError(errors, "items", "At least one variant change is required.");
        }

        var liveVariants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == productId)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .ToListAsync(cancellationToken);
        var byId = liveVariants.ToDictionary(variant => variant.Id);
        var finalVariants = liveVariants
            .Select(ProductVariantRevisionFinalVariant.FromLiveVariant)
            .ToDictionary(variant => variant.SourceVariantId!.Value);
        var changedSourceIds = new HashSet<Guid>();
        var deactivateSourceIds = new HashSet<Guid>();

        foreach (var item in items)
        {
            ValidateItemShape(item, errors);

            if (item.Operation == ProductVariantRevisionItemOperation.Add)
            {
                finalVariants[Guid.NewGuid()] = ProductVariantRevisionFinalVariant.FromNewItem(item);
                continue;
            }

            if (!item.SourceVariantId.HasValue || !byId.TryGetValue(item.SourceVariantId.Value, out var source))
            {
                AddError(errors, "items", $"Source variant {item.SourceVariantId} was not found on this product.");
                continue;
            }

            if (!changedSourceIds.Add(source.Id))
            {
                AddError(errors, "items", $"Variant {source.Sku} is staged more than once.");
                continue;
            }

            if (item.Operation == ProductVariantRevisionItemOperation.Deactivate)
            {
                deactivateSourceIds.Add(source.Id);
                finalVariants[source.Id] = ProductVariantRevisionFinalVariant.FromLiveVariant(source) with
                {
                    Status = ProductVariantStatus.Inactive,
                    ChangeType = item.Operation.ToString()
                };
                continue;
            }

            finalVariants[source.Id] = ProductVariantRevisionFinalVariant.FromUpdateItem(item, source);
        }

        ValidateFinalUniqueness(finalVariants.Values, errors);
        ValidateFinalSellableState(finalVariants.Values, errors);

        if (deactivateSourceIds.Count > 0)
        {
            await ValidateDeactivateSafetyAsync(deactivateSourceIds, dbContext, errors, cancellationToken);
        }

        return new ProductVariantRevisionValidationResult(
            errors.ToDictionary(
                item => item.Key,
                item => item.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            finalVariants.Values
                .OrderBy(variant => variant.Size)
                .ThenBy(variant => variant.Colour)
                .ThenBy(variant => variant.Sku)
                .ToArray());
    }

    private static void ValidateItemShape(
        ProductVariantRevisionItem item,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(item.Sku))
        {
            AddError(errors, "sku", "SKU is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Size))
        {
            AddError(errors, "size", "Size is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Colour))
        {
            AddError(errors, "colour", "Colour is required.");
        }

        if (item.Price <= 0)
        {
            AddError(errors, "price", "Price must be positive.");
        }

        if (item.CompareAtPrice.HasValue && item.CompareAtPrice <= item.Price)
        {
            AddError(errors, "compareAtPrice", "Compare-at price must be greater than price.");
        }

        if (item.Operation == ProductVariantRevisionItemOperation.Add && item.InitialStockQuantity is null or < 0)
        {
            AddError(errors, "initialStockQuantity", "New variant initial stock must be zero or greater.");
        }
    }

    private static void ValidateFinalUniqueness(
        IEnumerable<ProductVariantRevisionFinalVariant> variants,
        Dictionary<string, List<string>> errors)
    {
        var duplicateSku = variants
            .GroupBy(variant => variant.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSku is not null)
        {
            AddError(errors, "sku", $"SKU '{duplicateSku.Key}' appears more than once in the proposed final variant set.");
        }

        var duplicateSizeColour = variants
            .GroupBy(
                variant => $"{variant.Size.Trim()}::{variant.Colour.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSizeColour is not null)
        {
            AddError(errors, "sizeColour", "Each proposed size and colour combination must be unique for the product.");
        }
    }

    private static void ValidateFinalSellableState(
        IEnumerable<ProductVariantRevisionFinalVariant> variants,
        Dictionary<string, List<string>> errors)
    {
        if (!variants.Any(variant => variant.Status == ProductVariantStatus.Active
                && variant.StockQuantity > variant.ReservedQuantity))
        {
            AddError(errors, "items", "The approved product must keep at least one active sellable variant.");
        }
    }

    private static async Task ValidateDeactivateSafetyAsync(
        HashSet<Guid> deactivateSourceIds,
        MabuntleDbContext dbContext,
        Dictionary<string, List<string>> errors,
        CancellationToken cancellationToken)
    {
        var reservedVariantIds = await dbContext.InventoryReservations
            .Where(reservation => deactivateSourceIds.Contains(reservation.ProductVariantId)
                && reservation.Status == InventoryReservationStatus.Active)
            .Select(reservation => reservation.ProductVariantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (reservedVariantIds.Count > 0)
        {
            AddError(errors, "items", "Variants with active reservations cannot be deactivated.");
        }

        var activeCartVariantIds = await dbContext.CartItems
            .Where(item => deactivateSourceIds.Contains(item.ProductVariantId))
            .Join(
                dbContext.Carts.Where(cart => cart.Status == CartStatus.Active),
                item => item.CartId,
                cart => cart.Id,
                (item, _) => item.ProductVariantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (activeCartVariantIds.Count > 0)
        {
            AddError(errors, "items", "Variants in active carts cannot be deactivated.");
        }
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }
}

internal sealed record ProductVariantRevisionValidationResult(
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyCollection<ProductVariantRevisionFinalVariant> FinalVariants)
{
    public bool IsValid => Errors.Count == 0;
}

internal sealed record ProductVariantRevisionFinalVariant(
    Guid? SourceVariantId,
    string ChangeType,
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    int ReservedQuantity,
    ProductVariantStatus Status,
    string? Barcode)
{
    public int AvailableQuantity => StockQuantity - ReservedQuantity;

    public static ProductVariantRevisionFinalVariant FromLiveVariant(ProductVariant variant) =>
        new(
            variant.Id,
            "Live",
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            variant.CompareAtPrice,
            variant.StockQuantity,
            variant.ReservedQuantity,
            variant.Status,
            variant.Barcode);

    public static ProductVariantRevisionFinalVariant FromNewItem(ProductVariantRevisionItem item) =>
        new(
            null,
            item.Operation.ToString(),
            item.Sku,
            item.Size,
            item.Colour,
            item.Price,
            item.CompareAtPrice,
            item.InitialStockQuantity ?? 0,
            0,
            ProductVariantStatus.Active,
            item.Barcode);

    public static ProductVariantRevisionFinalVariant FromUpdateItem(
        ProductVariantRevisionItem item,
        ProductVariant source) =>
        new(
            source.Id,
            item.Operation.ToString(),
            item.Sku,
            item.Size,
            item.Colour,
            item.Price,
            item.CompareAtPrice,
            source.StockQuantity,
            source.ReservedQuantity,
            source.Status,
            item.Barcode);
}
