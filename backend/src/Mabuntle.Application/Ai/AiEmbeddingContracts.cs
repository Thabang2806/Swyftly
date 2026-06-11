namespace Mabuntle.Application.Ai;

public interface IAiEmbeddingService
{
    Task<AiEmbeddingResponse> GenerateEmbeddingAsync(
        AiEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProductEmbeddingGenerator
{
    Task GenerateForProductAsync(Guid productId, CancellationToken cancellationToken = default);
}

public sealed record AiEmbeddingRequest(string SourceText);

public sealed record AiEmbeddingResponse(
    IReadOnlyList<float> Values,
    string ModelUsed);
