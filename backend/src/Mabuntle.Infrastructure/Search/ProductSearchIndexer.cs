using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Search;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Search;

public sealed class ProductSearchIndexer(
    MabuntleDbContext dbContext,
    ISearchIndexService searchIndexService) : IProductSearchIndexer
{
    public async Task IndexProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var document = await BuildDocumentAsync(productId, cancellationToken);
        if (document is null)
        {
            await searchIndexService.RemoveProductAsync(productId, cancellationToken);
            return;
        }

        await searchIndexService.IndexProductAsync(document, cancellationToken);
    }

    public Task RemoveProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        searchIndexService.RemoveProductAsync(productId, cancellationToken);

    private async Task<ProductSearchDocument?> BuildDocumentAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
        {
            return null;
        }

        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
            .ToListAsync(cancellationToken);
        if (variants.Count == 0)
        {
            return null;
        }

        var attributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        var materials = attributes
            .Where(attribute => attribute.Key == "material")
            .Select(attribute => ReadSearchValue(attribute.ValueJson))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var descriptionParts = new[]
            {
                product.ShortDescription,
                product.FullDescription,
                product.SeoTitle,
                product.SeoDescription,
                product.MerchandisingLabel,
                product.CareInstructions,
                product.ProductDisclaimer
            }
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return new ProductSearchDocument(
            product.Id,
            product.SellerId,
            product.Title,
            string.Join(" ", descriptionParts),
            await GetCategoryPathAsync(product.CategoryId, cancellationToken),
            Brand: null,
            variants.Min(variant => variant.Price),
            variants.Max(variant => variant.Price),
            variants.Select(variant => variant.Size).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            variants.Select(variant => variant.Colour).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            materials,
            ReadStringArray(product.TagsJson),
            variants.Any(variant => variant.StockQuantity > variant.ReservedQuantity),
            product.PublishedAtUtc);
    }

    private async Task<string?> GetCategoryPathAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var categories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var byId = categories.ToDictionary(category => category.Id);
        var names = new Stack<string>();
        var currentId = categoryId;

        while (currentId.HasValue && byId.TryGetValue(currentId.Value, out var category))
        {
            names.Push(category.Name);
            currentId = category.ParentCategoryId;
        }

        return names.Count == 0 ? null : string.Join(" > ", names);
    }

    private static IReadOnlyCollection<string> ReadStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ReadSearchValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.String
            ? document.RootElement.GetString()
            : document.RootElement.GetRawText();
    }
}
