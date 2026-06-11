namespace Mabuntle.Application.Search;

public interface ISearchIndexService
{
    Task IndexProductAsync(ProductSearchDocument document, CancellationToken cancellationToken = default);

    Task RemoveProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductSearchIndexResult?> SearchProductsAsync(ProductSearchIndexQuery query, CancellationToken cancellationToken = default);
}

public interface IProductSearchIndexer
{
    Task IndexProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task RemoveProductAsync(Guid productId, CancellationToken cancellationToken = default);
}

public sealed record ProductSearchDocument(
    Guid ProductId,
    Guid SellerId,
    string? Title,
    string? Description,
    string? CategoryPath,
    string? Brand,
    decimal PriceMin,
    decimal PriceMax,
    IReadOnlyCollection<string> Sizes,
    IReadOnlyCollection<string> Colours,
    IReadOnlyCollection<string> Materials,
    IReadOnlyCollection<string> Tags,
    bool InStock,
    DateTimeOffset? PublishedAtUtc);

public sealed record ProductSearchIndexQuery(
    string? Query,
    Guid? CategoryId,
    string? CategoryPath,
    Guid? SellerId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Size,
    string? Colour,
    Guid? BrandId,
    string? Material,
    bool? InStock,
    string Sort,
    int Page,
    int PageSize);

public sealed record ProductSearchIndexResult(
    IReadOnlyCollection<Guid> ProductIds,
    int TotalCount,
    string ProviderName);
