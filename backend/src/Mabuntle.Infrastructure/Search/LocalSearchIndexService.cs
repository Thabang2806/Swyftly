using System.Collections.Concurrent;
using Mabuntle.Application.Search;

namespace Mabuntle.Infrastructure.Search;

public sealed class LocalSearchIndexService : ISearchIndexService
{
    private readonly ConcurrentDictionary<Guid, ProductSearchDocument> _documents = new();

    public Task IndexProductAsync(ProductSearchDocument document, CancellationToken cancellationToken = default)
    {
        _documents[document.ProductId] = document;
        return Task.CompletedTask;
    }

    public Task RemoveProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        _documents.TryRemove(productId, out _);
        return Task.CompletedTask;
    }

    public Task<ProductSearchIndexResult?> SearchProductsAsync(
        ProductSearchIndexQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_documents.IsEmpty)
        {
            return Task.FromResult<ProductSearchIndexResult?>(null);
        }

        var documents = _documents.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var normalized = query.Query.Trim();
            documents = documents.Where(document =>
                Contains(document.Title, normalized) ||
                Contains(document.Description, normalized) ||
                Contains(document.CategoryPath, normalized) ||
                document.Tags.Any(tag => Contains(tag, normalized)));
        }

        if (query.SellerId.HasValue)
        {
            documents = documents.Where(document => document.SellerId == query.SellerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryPath))
        {
            documents = documents.Where(document => string.Equals(
                document.CategoryPath,
                query.CategoryPath,
                StringComparison.OrdinalIgnoreCase));
        }

        if (query.MinPrice.HasValue)
        {
            documents = documents.Where(document => document.PriceMax >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            documents = documents.Where(document => document.PriceMin <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Size))
        {
            documents = documents.Where(document => document.Sizes.Contains(query.Size.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Colour))
        {
            documents = documents.Where(document => document.Colours.Contains(query.Colour.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Material))
        {
            documents = documents.Where(document => document.Materials.Contains(query.Material.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        if (query.InStock.HasValue)
        {
            documents = documents.Where(document => document.InStock == query.InStock.Value);
        }

        documents = query.Sort switch
        {
            "price_asc" => documents.OrderBy(document => document.PriceMin).ThenByDescending(document => document.PublishedAtUtc),
            "price_desc" => documents.OrderByDescending(document => document.PriceMax).ThenByDescending(document => document.PublishedAtUtc),
            _ => documents.OrderByDescending(document => document.PublishedAtUtc)
        };

        var totalCount = documents.Count();
        var productIds = documents
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(document => document.ProductId)
            .ToArray();

        return Task.FromResult<ProductSearchIndexResult?>(new ProductSearchIndexResult(
            productIds,
            totalCount,
            "local-memory"));
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
