using Mabuntle.Application.Ai;
using Mabuntle.Domain.Ai;
using Mabuntle.Infrastructure.Ai;

namespace Mabuntle.UnitTests.Infrastructure;

public class FakeAiEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsStableNormalizedEmbedding()
    {
        var service = new FakeAiEmbeddingService();

        var first = await service.GenerateEmbeddingAsync(new AiEmbeddingRequest("Cotton Summer Dress"));
        var second = await service.GenerateEmbeddingAsync(new AiEmbeddingRequest(" cotton summer dress "));

        Assert.Equal(FakeAiEmbeddingService.ModelName, first.ModelUsed);
        Assert.Equal(ProductEmbedding.EmbeddingDimension, first.Values.Count);
        Assert.Equal(first.Values, second.Values);
        Assert.InRange(
            Math.Sqrt(first.Values.Sum(value => value * value)),
            0.999d,
            1.001d);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RequiresSourceText()
    {
        var service = new FakeAiEmbeddingService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateEmbeddingAsync(new AiEmbeddingRequest(" ")));
    }
}
