using Microsoft.EntityFrameworkCore;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Ai;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class ProductEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateForProductAsync_PersistsEmbeddingWithSearchableSourceText()
    {
        await using var dbContext = CreateDbContext();
        var product = CreatePublishedProduct();
        dbContext.Categories.AddRange(CatalogSeedData.CreateCategories());
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            "SKU-1",
            "M",
            "Black",
            499m,
            699m,
            10));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
        await dbContext.SaveChangesAsync();
        var generator = new ProductEmbeddingGenerator(
            dbContext,
            new FakeAiEmbeddingService(),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-18T12:00:00Z")));

        await generator.GenerateForProductAsync(product.Id);

        var embedding = await dbContext.ProductEmbeddings.SingleAsync();
        Assert.Equal(product.Id, embedding.ProductId);
        Assert.Equal(FakeAiEmbeddingService.ModelName, embedding.ModelUsed);
        Assert.Equal(ProductEmbedding.EmbeddingDimension, embedding.Embedding.ToArray().Length);
        Assert.Contains("Cotton Summer Dress", embedding.SourceText);
        Assert.Contains("Women > Clothing > Dresses", embedding.SourceText);
        Assert.Contains("Attribute material: Cotton", embedding.SourceText);
        Assert.Contains("summer", embedding.SourceText);
        Assert.Contains("M", embedding.SourceText);
        Assert.Contains("Black", embedding.SourceText);
        Assert.Contains("SKU-1", embedding.SourceText);
    }

    [Fact]
    public async Task GenerateForProductAsync_UpsertsExistingProductModelEmbedding()
    {
        await using var dbContext = CreateDbContext();
        var product = CreatePublishedProduct();
        dbContext.Categories.AddRange(CatalogSeedData.CreateCategories());
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            "SKU-1",
            "M",
            "Black",
            499m,
            699m,
            10));
        await dbContext.SaveChangesAsync();
        var generator = new ProductEmbeddingGenerator(
            dbContext,
            new FakeAiEmbeddingService(),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-18T12:00:00Z")));

        await generator.GenerateForProductAsync(product.Id);
        await generator.GenerateForProductAsync(product.Id);

        Assert.Equal(1, await dbContext.ProductEmbeddings.CountAsync());
    }

    private static Product CreatePublishedProduct()
    {
        var product = new Product(Guid.NewGuid());
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            "Cotton Summer Dress",
            "cotton-summer-dress",
            "A cotton summer dress.",
            "A breathable dress for warm weather.");
        product.UpdateTags("[\"summer\",\"cotton\"]");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.Parse("2026-05-18T11:00:00Z"));
        return product;
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"ProductEmbeddingGeneratorTests-{Guid.NewGuid():N}")
            .Options;

        return new MabuntleDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
