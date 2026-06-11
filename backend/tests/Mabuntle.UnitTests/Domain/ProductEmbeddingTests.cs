using Pgvector;
using Mabuntle.Domain.Ai;

namespace Mabuntle.UnitTests.Domain;

public class ProductEmbeddingTests
{
    [Fact]
    public void ProductEmbedding_StoresRequiredEmbeddingMetadata()
    {
        var productId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var vector = new Vector(new float[ProductEmbedding.EmbeddingDimension]);

        var embedding = new ProductEmbedding(
            productId,
            "Cotton summer dress",
            vector,
            "local-fake-embedding-v1",
            createdAt);

        Assert.Equal(productId, embedding.ProductId);
        Assert.Equal("Cotton summer dress", embedding.SourceText);
        Assert.Equal("local-fake-embedding-v1", embedding.ModelUsed);
        Assert.Equal(createdAt, embedding.CreatedAtUtc);
        Assert.Equal(ProductEmbedding.EmbeddingDimension, embedding.Embedding.ToArray().Length);
    }

    [Fact]
    public void ProductEmbedding_RejectsWrongVectorDimension()
    {
        Assert.Throws<ArgumentException>(() => new ProductEmbedding(
            Guid.NewGuid(),
            "Cotton summer dress",
            new Vector(new float[3]),
            "local-fake-embedding-v1",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Replace_UpdatesSourceVectorAndTimestamp()
    {
        var embedding = new ProductEmbedding(
            Guid.NewGuid(),
            "Initial source",
            new Vector(new float[ProductEmbedding.EmbeddingDimension]),
            "local-fake-embedding-v1",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        var updatedAt = DateTimeOffset.Parse("2026-05-18T13:00:00Z");
        var updatedVector = Enumerable.Repeat(0.1f, ProductEmbedding.EmbeddingDimension).ToArray();

        embedding.Replace("Updated source", new Vector(updatedVector), updatedAt);

        Assert.Equal("Updated source", embedding.SourceText);
        Assert.Equal(updatedAt, embedding.CreatedAtUtc);
        Assert.Equal(updatedVector, embedding.Embedding.ToArray());
    }
}
