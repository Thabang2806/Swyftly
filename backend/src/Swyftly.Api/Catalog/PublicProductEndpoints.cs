using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Security;
using Swyftly.Api.Sellers;
using Swyftly.Application.Search;
using Swyftly.Application.Sellers;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Catalog;

public static class PublicProductEndpoints
{
    private const int DefaultPageSize = 24;
    private const int MaxPageSize = 60;

    public static IEndpointRouteBuilder MapPublicProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/search", SearchAsync)
            .WithTags("Public Products")
            .WithName("SearchProducts")
            .WithSummary("Searches published products using PostgreSQL-backed filters.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.Search)
            .Produces<ProductSearchResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        app.MapGet("/api/products/{slug}", GetBySlugAsync)
            .WithTags("Public Products")
            .WithName("GetPublicProductBySlug")
            .WithSummary("Returns public product detail by slug.")
            .Produces<PublicProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/categories", ListCategoriesAsync)
            .WithTags("Public Catalog")
            .WithName("ListPublicCategories")
            .WithSummary("Returns public active categories for browsing.")
            .Produces<IReadOnlyCollection<PublicCategoryResponse>>(StatusCodes.Status200OK);

        app.MapGet("/api/sellers/{storeSlug}", GetSellerStorefrontAsync)
            .WithTags("Public Sellers")
            .WithName("GetPublicSellerStorefront")
            .WithSummary("Returns public seller storefront detail and published products.")
            .Produces<PublicSellerStorefrontResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        Guid? categoryId,
        string? categorySlug,
        Guid? sellerId,
        decimal? minPrice,
        decimal? maxPrice,
        string? size,
        string? colour,
        Guid? brandId,
        string? material,
        bool? inStock,
        string? sort,
        int? page,
        int? pageSize,
        SwyftlyDbContext dbContext,
        ISearchIndexService searchIndexService,
        CancellationToken cancellationToken)
    {
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
        {
            return Validation("price", "minPrice must be less than or equal to maxPrice.");
        }

        var resolvedCategoryId = categoryId;
        if (!resolvedCategoryId.HasValue && !string.IsNullOrWhiteSpace(categorySlug))
        {
            resolvedCategoryId = await dbContext.Categories
                .Where(category => category.Slug == categorySlug.Trim().ToLowerInvariant() && category.IsActive)
                .Select(category => (Guid?)category.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var pageNumber = Math.Max(1, page ?? 1);
        var sizeValue = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var sortValue = NormalizeSort(sort);
        if (sortValue is null)
        {
            return Validation("sort", "Sort must be newest, price_asc, price_desc, or relevance.");
        }

        var categoryPath = await GetCategoryPathAsync(resolvedCategoryId, dbContext, cancellationToken);
        var indexQuery = new ProductSearchIndexQuery(
            query,
            resolvedCategoryId,
            categoryPath,
            sellerId,
            minPrice,
            maxPrice,
            size,
            colour,
            brandId,
            material,
            inStock,
            sortValue,
            pageNumber,
            sizeValue);

        var indexResult = await searchIndexService.SearchProductsAsync(indexQuery, cancellationToken);
        if (indexResult is not null)
        {
            var indexedItems = new List<ProductSearchItemResponse>();
            foreach (var productId in indexResult.ProductIds)
            {
                var card = await CreateProductCardAsync(productId, dbContext, cancellationToken);
                if (card is not null)
                {
                    indexedItems.Add(card);
                }
            }

            return HttpResults.Ok(new ProductSearchResponse(
                indexedItems,
                pageNumber,
                sizeValue,
                indexResult.TotalCount,
                sortValue));
        }

        var products = BuildSearchQuery(
            dbContext,
            query,
            resolvedCategoryId,
            sellerId,
            minPrice,
            maxPrice,
            size,
            colour,
            brandId,
            material,
            inStock);

        products = ApplySort(products, dbContext, sortValue);

        var totalCount = await products.CountAsync(cancellationToken);
        var productIds = await products
            .Skip((pageNumber - 1) * sizeValue)
            .Take(sizeValue)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        var items = new List<ProductSearchItemResponse>();
        foreach (var productId in productIds)
        {
            var card = await CreateProductCardAsync(productId, dbContext, cancellationToken);
            if (card is not null)
            {
                items.Add(card);
            }
        }

        return HttpResults.Ok(new ProductSearchResponse(
            items,
            pageNumber,
            sizeValue,
            totalCount,
            sortValue));
    }

    private static async Task<IResult> GetBySlugAsync(
        string slug,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var productId = await BuildVisiblePublishedProductQuery(dbContext)
            .Where(product => product.Slug == normalizedSlug)
            .OrderByDescending(product => product.PublishedAtUtc)
            .Select(product => (Guid?)product.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!productId.HasValue)
        {
            return HttpResults.Problem(
                title: "Products.NotFound",
                detail: "Product was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return HttpResults.Ok(await CreateProductDetailAsync(productId.Value, dbContext, cancellationToken));
    }

    private static async Task<IResult> ListCategoriesAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.ParentCategoryId == null ? 0 : 1)
            .ThenBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new PublicCategoryResponse(
                category.Id,
                category.ParentCategoryId,
                category.Name,
                category.Slug,
                category.DisplayOrder))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(categories);
    }

    private static async Task<IResult> GetSellerStorefrontAsync(
        string storeSlug,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = storeSlug.Trim().ToLowerInvariant();
        var storefront = await dbContext.SellerStorefronts
            .AsNoTracking()
            .Where(store => store.Slug == normalizedSlug && store.IsPublished)
            .Join(
                dbContext.SellerProfiles.AsNoTracking().Where(seller => seller.VerificationStatus == SellerVerificationStatus.Verified),
                store => store.SellerId,
                seller => seller.Id,
                (store, _) => store)
            .SingleOrDefaultAsync(cancellationToken);
        if (storefront is null)
        {
            return HttpResults.Problem(
                title: "Sellers.StorefrontNotFound",
                detail: "Seller storefront was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var productIds = await dbContext.Products
            .Where(product => product.SellerId == storefront.SellerId && product.Status == ProductStatus.Published)
            .OrderByDescending(product => product.PublishedAtUtc)
            .Select(product => product.Id)
            .Take(24)
            .ToListAsync(cancellationToken);
        var products = new List<ProductSearchItemResponse>();
        foreach (var productId in productIds)
        {
            var card = await CreateProductCardAsync(productId, dbContext, cancellationToken);
            if (card is not null)
            {
                products.Add(card);
            }
        }
        var policy = await dbContext.SellerStorePolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SellerId == storefront.SellerId, cancellationToken);

        return HttpResults.Ok(new PublicSellerStorefrontResponse(
            storefront.SellerId,
            storefront.StoreName,
            storefront.Slug,
            storefront.Description,
            storefront.LogoUrl,
            storefront.BannerUrl,
            products,
            SellerPolicyResponseMapper.Map(policy)));
    }

    private static IQueryable<Product> BuildSearchQuery(
        SwyftlyDbContext dbContext,
        string? query,
        Guid? categoryId,
        Guid? sellerId,
        decimal? minPrice,
        decimal? maxPrice,
        string? size,
        string? colour,
        Guid? brandId,
        string? material,
        bool? inStock)
    {
        var products = BuildVisiblePublishedProductQuery(dbContext);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToLowerInvariant();
            products = products.Where(product =>
                (product.Title != null && product.Title.ToLower().Contains(normalized)) ||
                (product.ShortDescription != null && product.ShortDescription.ToLower().Contains(normalized)) ||
                (product.FullDescription != null && product.FullDescription.ToLower().Contains(normalized)) ||
                (product.SeoTitle != null && product.SeoTitle.ToLower().Contains(normalized)) ||
                (product.SeoDescription != null && product.SeoDescription.ToLower().Contains(normalized)) ||
                (product.MerchandisingLabel != null && product.MerchandisingLabel.ToLower().Contains(normalized)) ||
                (product.CareInstructions != null && product.CareInstructions.ToLower().Contains(normalized)) ||
                (product.ProductDisclaimer != null && product.ProductDisclaimer.ToLower().Contains(normalized)) ||
                product.TagsJson.ToLower().Contains(normalized));
        }

        if (categoryId.HasValue)
        {
            products = products.Where(product => product.CategoryId == categoryId);
        }

        if (sellerId.HasValue)
        {
            products = products.Where(product => product.SellerId == sellerId);
        }

        if (brandId.HasValue)
        {
            products = products.Where(product => product.BrandId == brandId);
        }

        if (minPrice.HasValue)
        {
            products = products.Where(product => dbContext.ProductVariants.Any(variant =>
                variant.ProductId == product.Id &&
                variant.Status == ProductVariantStatus.Active &&
                variant.Price >= minPrice.Value));
        }

        if (maxPrice.HasValue)
        {
            products = products.Where(product => dbContext.ProductVariants.Any(variant =>
                variant.ProductId == product.Id &&
                variant.Status == ProductVariantStatus.Active &&
                variant.Price <= maxPrice.Value));
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            var normalized = size.Trim().ToLowerInvariant();
            products = products.Where(product => dbContext.ProductVariants.Any(variant =>
                variant.ProductId == product.Id &&
                variant.Status == ProductVariantStatus.Active &&
                variant.Size.ToLower() == normalized));
        }

        if (!string.IsNullOrWhiteSpace(colour))
        {
            var normalized = colour.Trim().ToLowerInvariant();
            products = products.Where(product => dbContext.ProductVariants.Any(variant =>
                variant.ProductId == product.Id &&
                variant.Status == ProductVariantStatus.Active &&
                variant.Colour.ToLower() == normalized));
        }

        if (!string.IsNullOrWhiteSpace(material))
        {
            var normalized = material.Trim().ToLowerInvariant();
            products = products.Where(product => dbContext.ProductAttributeValues.Any(attribute =>
                attribute.ProductId == product.Id &&
                attribute.Key == "material" &&
                attribute.ValueJson.ToLower().Contains(normalized)));
        }

        if (inStock.HasValue)
        {
            products = inStock.Value
                ? products.Where(product => dbContext.ProductVariants.Any(variant =>
                    variant.ProductId == product.Id &&
                    variant.Status == ProductVariantStatus.Active &&
                    variant.StockQuantity > variant.ReservedQuantity))
                : products.Where(product => !dbContext.ProductVariants.Any(variant =>
                    variant.ProductId == product.Id &&
                    variant.Status == ProductVariantStatus.Active &&
                    variant.StockQuantity > variant.ReservedQuantity));
        }

        return products;
    }

    private static IQueryable<Product> ApplySort(
        IQueryable<Product> products,
        SwyftlyDbContext dbContext,
        string sort) =>
        sort switch
        {
            "price_asc" => products
                .OrderBy(product => dbContext.ProductVariants
                    .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
                    .Min(variant => (decimal?)variant.Price) ?? decimal.MaxValue)
                .ThenByDescending(product => product.PublishedAtUtc),
            "price_desc" => products
                .OrderByDescending(product => dbContext.ProductVariants
                    .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
                    .Max(variant => (decimal?)variant.Price) ?? 0m)
                .ThenByDescending(product => product.PublishedAtUtc),
            _ => products.OrderByDescending(product => product.PublishedAtUtc)
        };

    private static async Task<ProductSearchItemResponse?> CreateProductCardAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await BuildVisiblePublishedProductQuery(dbContext)
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var storefront = await dbContext.SellerStorefronts.SingleOrDefaultAsync(
            storefront => storefront.SellerId == product.SellerId,
            cancellationToken);
        var primaryImage = await dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return null;
        }

        return new ProductSearchItemResponse(
            product.Id,
            product.SellerId,
            storefront?.StoreName,
            storefront?.Slug,
            product.CategoryId,
            await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
            product.BrandId,
            product.Title,
            product.Slug,
            product.ShortDescription,
            product.MerchandisingLabel,
            primaryImage?.Url,
            primaryImage?.AltText,
            variants.Min(variant => variant.Price),
            variants.Where(variant => variant.CompareAtPrice.HasValue).Min(variant => variant.CompareAtPrice),
            variants.Any(variant => variant.StockQuantity > variant.ReservedQuantity),
            ReadStringArray(product.TagsJson),
            product.PublishedAtUtc);
    }

    private static async Task<PublicProductDetailResponse> CreateProductDetailAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var card = await CreateProductCardAsync(productId, dbContext, cancellationToken)
            ?? throw new InvalidOperationException("Published product detail could not be created.");
        var product = await dbContext.Products.SingleAsync(product => product.Id == productId, cancellationToken);
        var images = await dbContext.ProductImages
            .Where(image => image.ProductId == productId)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select(image => new PublicProductImageResponse(
                image.Id,
                image.Url,
                image.AltText,
                image.IsPrimary))
            .ToListAsync(cancellationToken);
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == productId && variant.Status == ProductVariantStatus.Active)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new PublicProductVariantResponse(
                variant.Id,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity > variant.ReservedQuantity))
            .ToListAsync(cancellationToken);
        var attributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == productId)
            .OrderBy(attribute => attribute.Key)
            .ToDictionaryAsync(
                attribute => attribute.Key,
                attribute => attribute.ValueJson,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var policy = await dbContext.SellerStorePolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SellerId == product.SellerId, cancellationToken);

        return new PublicProductDetailResponse(
            card,
            product.FullDescription,
            product.SeoTitle,
            product.SeoDescription,
            product.CareInstructions,
            product.ProductDisclaimer,
            attributes,
            images,
            variants,
            SellerPolicyResponseMapper.Map(policy));
    }

    private static IQueryable<Product> BuildVisiblePublishedProductQuery(SwyftlyDbContext dbContext) =>
        dbContext.Products
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Published
                && dbContext.SellerProfiles.Any(seller =>
                    seller.Id == product.SellerId
                    && seller.VerificationStatus == SellerVerificationStatus.Verified)
                && dbContext.SellerStorefronts.Any(storefront =>
                    storefront.SellerId == product.SellerId
                    && storefront.IsPublished));

    private static async Task<string?> GetCategoryPathAsync(
        Guid? categoryId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
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

    private static string? NormalizeSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort)
            ? "newest"
            : sort.Trim().ToLowerInvariant();

        return normalized is "newest" or "price_asc" or "price_desc" or "relevance"
            ? normalized
            : null;
    }

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
}

public sealed record ProductSearchResponse(
    IReadOnlyCollection<ProductSearchItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Sort);

public sealed record ProductSearchItemResponse(
    Guid ProductId,
    Guid SellerId,
    string? SellerStoreName,
    string? SellerStoreSlug,
    Guid? CategoryId,
    string? CategoryPath,
    Guid? BrandId,
    string? Title,
    string? Slug,
    string? ShortDescription,
    string? MerchandisingLabel,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText,
    decimal PriceMin,
    decimal? CompareAtPriceMin,
    bool InStock,
    IReadOnlyCollection<string> Tags,
    DateTimeOffset? PublishedAtUtc);

public sealed record PublicProductDetailResponse(
    ProductSearchItemResponse Product,
    string? FullDescription,
    string? SeoTitle,
    string? SeoDescription,
    string? CareInstructions,
    string? ProductDisclaimer,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyCollection<PublicProductImageResponse> Images,
    IReadOnlyCollection<PublicProductVariantResponse> Variants,
    SellerPolicyResponse SellerPolicy);

public sealed record PublicProductImageResponse(
    Guid ImageId,
    string Url,
    string? AltText,
    bool IsPrimary);

public sealed record PublicProductVariantResponse(
    Guid VariantId,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    bool InStock);

public sealed record PublicCategoryResponse(
    Guid CategoryId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder);

public sealed record PublicSellerStorefrontResponse(
    Guid SellerId,
    string StoreName,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyCollection<ProductSearchItemResponse> Products,
    SellerPolicyResponse SellerPolicy);
