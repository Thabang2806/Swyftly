using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Mabuntle.Application.Ai;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Ai;

public sealed class ProductEmbeddingGenerator(
    MabuntleDbContext dbContext,
    IAiEmbeddingService embeddingService,
    TimeProvider timeProvider) : IProductEmbeddingGenerator
{
    public async Task GenerateForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
        {
            return;
        }

        var sourceText = await BuildSourceTextAsync(product, cancellationToken);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return;
        }

        var response = await embeddingService.GenerateEmbeddingAsync(new AiEmbeddingRequest(sourceText), cancellationToken);
        var embedding = new Vector(response.Values.ToArray());
        var existing = await dbContext.ProductEmbeddings.SingleOrDefaultAsync(
            item => item.ProductId == product.Id && item.ModelUsed == response.ModelUsed,
            cancellationToken);
        var generatedAtUtc = timeProvider.GetUtcNow();

        if (existing is null)
        {
            dbContext.ProductEmbeddings.Add(new ProductEmbedding(
                product.Id,
                sourceText,
                embedding,
                response.ModelUsed,
                generatedAtUtc));
        }
        else
        {
            existing.Replace(sourceText, embedding, generatedAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> BuildSourceTextAsync(Product product, CancellationToken cancellationToken)
    {
        var variants = await dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
            .ToListAsync(cancellationToken);
        var attributes = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.ProductId == product.Id)
            .OrderBy(attribute => attribute.Key)
            .ToListAsync(cancellationToken);
        var parts = new List<string?>
        {
            $"Title: {product.Title}",
            $"Short description: {product.ShortDescription}",
            $"Description: {product.FullDescription}",
            $"Category: {await GetCategoryPathAsync(product.CategoryId, cancellationToken)}",
            $"Tags: {string.Join(", ", ReadStringArray(product.TagsJson))}"
        };
        AddOptionalPart(parts, "SEO title", product.SeoTitle);
        AddOptionalPart(parts, "SEO description", product.SeoDescription);
        AddOptionalPart(parts, "Merchandising label", product.MerchandisingLabel);
        AddOptionalPart(parts, "Care instructions", product.CareInstructions);
        AddOptionalPart(parts, "Product disclaimer", product.ProductDisclaimer);

        parts.AddRange(attributes.Select(attribute =>
            $"Attribute {attribute.Key}: {ReadSearchValue(attribute.ValueJson)}"));

        if (variants.Count > 0)
        {
            parts.Add($"Sizes: {string.Join(", ", variants.Select(variant => variant.Size).Distinct(StringComparer.OrdinalIgnoreCase))}");
            parts.Add($"Colours: {string.Join(", ", variants.Select(variant => variant.Colour).Distinct(StringComparer.OrdinalIgnoreCase))}");
            parts.Add($"Variant SKUs: {string.Join(", ", variants.Select(variant => variant.Sku).Distinct(StringComparer.OrdinalIgnoreCase))}");
        }

        return string.Join(
            Environment.NewLine,
            parts.Where(part => !string.IsNullOrWhiteSpace(part)));
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

    private static void AddOptionalPart(List<string?> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}: {value.Trim()}");
        }
    }
}
