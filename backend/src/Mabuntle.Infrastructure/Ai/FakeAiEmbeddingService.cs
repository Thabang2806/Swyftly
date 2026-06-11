using System.Security.Cryptography;
using System.Text;
using Mabuntle.Application.Ai;
using Mabuntle.Domain.Ai;

namespace Mabuntle.Infrastructure.Ai;

public sealed class FakeAiEmbeddingService : IAiEmbeddingService
{
    public const string ModelName = "local-fake-embedding-v1";

    public Task<AiEmbeddingResponse> GenerateEmbeddingAsync(
        AiEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText))
        {
            throw new ArgumentException("Source text is required.", nameof(request));
        }

        var normalized = request.SourceText.Trim().ToLowerInvariant();
        var values = new float[ProductEmbedding.EmbeddingDimension];

        for (var offset = 0; offset < values.Length; offset += 32)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = SHA256.HashData(Encoding.UTF8.GetBytes($"{normalized}|{offset / 32}"));
            for (var index = 0; index < block.Length && offset + index < values.Length; index++)
            {
                values[offset + index] = (block[index] / 255f * 2f) - 1f;
            }
        }

        Normalize(values);
        return Task.FromResult(new AiEmbeddingResponse(values, ModelName));
    }

    private static void Normalize(float[] values)
    {
        var magnitudeSquared = 0d;
        foreach (var value in values)
        {
            magnitudeSquared += value * value;
        }

        if (magnitudeSquared <= 0)
        {
            return;
        }

        var magnitude = Math.Sqrt(magnitudeSquared);
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (float)(values[index] / magnitude);
        }
    }
}
