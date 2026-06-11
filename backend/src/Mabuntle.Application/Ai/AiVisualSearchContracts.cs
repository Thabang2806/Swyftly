using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Ai;

public interface IAiVisualSearchService
{
    Task<Result<VisualSearchAttributes>> ExtractAttributesAsync(
        VisualSearchExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiVisionProvider
{
    Task<VisualSearchAttributes> ExtractAttributesAsync(
        VisualSearchExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record VisualSearchExtractionRequest(
    string? ImageReference,
    string? ImageDataBase64,
    string? FileName,
    string? ContentType,
    string? UserId = null);

public sealed record VisualSearchAttributes(
    string? Category,
    string? Colour,
    string? Style,
    string? Shape,
    string? Pattern,
    string? MaterialGuess,
    decimal? MaterialConfidence,
    decimal Confidence,
    string SearchText,
    IReadOnlyCollection<string> Warnings);
