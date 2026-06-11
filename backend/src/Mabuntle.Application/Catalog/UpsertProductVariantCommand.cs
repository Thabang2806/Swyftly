using Mabuntle.Domain.Catalog;

namespace Mabuntle.Application.Catalog;

public sealed record UpsertProductVariantCommand(
    string Sku,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    int ReservedQuantity,
    ProductVariantStatus Status,
    string? Barcode);
