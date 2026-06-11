using Pgvector;
using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class ProductEmbedding : Entity
{
    public const int EmbeddingDimension = 1536;

    private ProductEmbedding()
    {
    }

    public ProductEmbedding(
        Guid productId,
        string sourceText,
        Vector embedding,
        string modelUsed,
        DateTimeOffset createdAtUtc)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        ProductId = productId;
        ModelUsed = Required(modelUsed, nameof(modelUsed));
        CreatedAtUtc = createdAtUtc;
        Replace(sourceText, embedding, createdAtUtc);
    }

    public Guid ProductId { get; private set; }

    public string SourceText { get; private set; } = string.Empty;

    public Vector Embedding { get; private set; } = new(new float[EmbeddingDimension]);

    public string ModelUsed { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Replace(string sourceText, Vector embedding, DateTimeOffset createdAtUtc)
    {
        SourceText = Required(sourceText, nameof(sourceText));
        Embedding = RequiredEmbedding(embedding);
        CreatedAtUtc = createdAtUtc;
    }

    private static Vector RequiredEmbedding(Vector? embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.ToArray().Length != EmbeddingDimension)
        {
            throw new ArgumentException(
                $"Embedding must have {EmbeddingDimension} dimensions.",
                nameof(embedding));
        }

        return embedding;
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
}
