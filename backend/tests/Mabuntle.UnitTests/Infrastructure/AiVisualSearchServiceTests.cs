using Mabuntle.Application.Ai;
using Mabuntle.Infrastructure.Ai;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class AiVisualSearchServiceTests
{
    [Fact]
    public async Task ExtractAttributesAsync_ExtractsDeterministicVisualAttributesFromReference()
    {
        var service = CreateService();

        var result = await service.ExtractAttributesAsync(new VisualSearchExtractionRequest(
            "black formal maxi dress flatlay",
            ImageDataBase64: null,
            FileName: null,
            ContentType: null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Dresses", result.Value.Category);
        Assert.Equal("Black", result.Value.Colour);
        Assert.Equal("Formal", result.Value.Style);
        Assert.Equal("Maxi", result.Value.Shape);
        Assert.Contains("Dresses", result.Value.SearchText);
    }

    [Fact]
    public async Task ExtractAttributesAsync_KeepsMaterialAsLowConfidenceGuess()
    {
        var service = CreateService();

        var result = await service.ExtractAttributesAsync(new VisualSearchExtractionRequest(
            "brown leather sneaker",
            ImageDataBase64: null,
            FileName: "leather-sneaker.webp",
            ContentType: null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Leather", result.Value.MaterialGuess);
        Assert.Equal(0.45m, result.Value.MaterialConfidence);
        Assert.Contains(result.Value.Warnings, warning => warning.Contains("low-confidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAttributesAsync_ReturnsValidationWhenNoImageInputExists()
    {
        var service = CreateService();

        var result = await service.ExtractAttributesAsync(new VisualSearchExtractionRequest(
            ImageReference: null,
            ImageDataBase64: null,
            FileName: null,
            ContentType: null));

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Type.ToString());
    }

    [Fact]
    public async Task ExtractAttributesAsync_RejectsUnsupportedContentType()
    {
        var service = CreateService();

        var result = await service.ExtractAttributesAsync(new VisualSearchExtractionRequest(
            ImageReference: null,
            ImageDataBase64: "abc123",
            FileName: "visual.txt",
            ContentType: "text/plain"));

        Assert.True(result.IsFailure);
        Assert.Contains("contentType", result.Error.Details!.Keys);
    }

    private static AiVisualSearchService CreateService() =>
        new(new FakeAiVisionProvider());
}
