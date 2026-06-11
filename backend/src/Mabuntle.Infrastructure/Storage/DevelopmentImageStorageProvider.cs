using Mabuntle.Application.Abstractions;

namespace Mabuntle.Infrastructure.Storage;

public sealed class DevelopmentImageStorageProvider : IImageStorageProvider
{
    public Task<ImageStorageReference> CreateReferenceAsync(
        CreateImageReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var storageKey = Required(request.StorageKey, nameof(request.StorageKey));
        var url = string.IsNullOrWhiteSpace(request.Url)
            ? $"/assets/product-images/{Uri.EscapeDataString(storageKey)}"
            : request.Url.Trim();

        return Task.FromResult(new ImageStorageReference(storageKey, url));
    }

    public Task<ImageStorageReference> UploadAsync(
        UploadImageStorageRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Development image references do not support binary uploads.");
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ImageStorageReadinessResult> CheckReadinessAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ImageStorageReadinessResult.Ready("Development image reference provider is available."));

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
