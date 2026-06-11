using Mabuntle.Application.Ai;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;

namespace Mabuntle.Infrastructure.Ai;

public sealed class AiVisualSearchService(IAiVisionProvider provider) : IAiVisualSearchService
{
    private const int MaxImageDataCharacters = 3_000_000;
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<Result<VisualSearchAttributes>> ExtractAttributesAsync(
        VisualSearchExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = Validate(request);
        if (failures.Count > 0)
        {
            return Result<VisualSearchAttributes>.Failure(Error.Validation(failures));
        }

        try
        {
            var attributes = await provider.ExtractAttributesAsync(request, cancellationToken);
            return Result<VisualSearchAttributes>.Success(attributes);
        }
        catch (Exception exception)
        {
            return Result<VisualSearchAttributes>.Failure(Error.Failure(
                "AiVisualSearch.ProviderFailed",
                $"The AI vision provider failed: {exception.Message}"));
        }
    }

    private static IReadOnlyCollection<ValidationFailure> Validate(VisualSearchExtractionRequest request)
    {
        var failures = new List<ValidationFailure>();
        var hasReference = !string.IsNullOrWhiteSpace(request.ImageReference);
        var hasImageData = !string.IsNullOrWhiteSpace(request.ImageDataBase64);

        if (!hasReference && !hasImageData)
        {
            failures.Add(new("image", "Provide an image upload or image reference."));
        }

        if (request.ImageDataBase64?.Length > MaxImageDataCharacters)
        {
            failures.Add(new("imageDataBase64", "Image uploads are limited to approximately 2 MB for the MVP."));
        }

        if (hasImageData && !string.IsNullOrWhiteSpace(request.ContentType))
        {
            var contentType = request.ContentType.Trim().ToLowerInvariant();
            if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add(new("contentType", "Only JPEG, PNG, or WebP images are supported."));
            }
        }

        return failures;
    }
}
