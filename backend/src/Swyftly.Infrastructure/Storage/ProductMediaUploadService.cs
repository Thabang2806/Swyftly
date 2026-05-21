using Microsoft.Extensions.Options;
using Swyftly.Application.Abstractions;
using Swyftly.Application.Media;
using Swyftly.Domain.Media;

namespace Swyftly.Infrastructure.Storage;

public sealed class ProductMediaUploadService(
    IImageStorageProvider storageProvider,
    IMediaMalwareScanner malwareScanner,
    MediaImageProcessor imageProcessor,
    IOptions<ImageStorageOptions> options,
    TimeProvider timeProvider) : IProductMediaUploadService
{
    private readonly ImageStorageOptions options = options.Value;

    public async Task<ProductMediaUploadResult> UploadAsync(
        ProductMediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Content is null)
        {
            throw new ArgumentException("Image content is required.", nameof(request));
        }

        var bytes = await ReadContentAsync(request.Content, request.Length, cancellationToken);
        var processed = imageProcessor.Process(bytes, request.ContentType);
        var scan = await malwareScanner.ScanAsync(
            new MediaScanRequest(request.FileName, processed.ContentType, bytes.Length, bytes),
            cancellationToken);

        if (!scan.IsClean)
        {
            throw new InvalidOperationException(scan.Reason ?? "Image failed malware scanning.");
        }

        var uploadedKeys = new List<string>();
        try
        {
            await using var originalStream = new MemoryStream(bytes);
            var originalReference = await storageProvider.UploadAsync(
                new UploadImageStorageRequest(
                    originalStream,
                    request.FileName,
                    processed.ContentType,
                    bytes.Length,
                    $"{request.Scope}/original"),
                cancellationToken);
            uploadedKeys.Add(originalReference.StorageKey);

            var now = timeProvider.GetUtcNow();
            var asset = new MediaAsset(
                request.SellerId,
                request.ProductId,
                request.ProductListingRevisionId,
                NormalizeProviderName(options.ProviderName),
                ResolveBucketName(),
                originalReference.StorageKey,
                originalReference.Url,
                request.FileName,
                processed.ContentType,
                bytes.Length,
                processed.Sha256Hash,
                processed.Width,
                processed.Height,
                MediaScanStatus.Clean,
                now,
                now);

            var variants = new List<MediaAssetVariant>();
            foreach (var variant in processed.Variants)
            {
                await using var variantStream = new MemoryStream(variant.Content);
                var reference = await storageProvider.UploadAsync(
                    new UploadImageStorageRequest(
                        variantStream,
                        $"{variant.Kind.ToString().ToLowerInvariant()}.webp",
                        variant.ContentType,
                        variant.Content.Length,
                        $"{request.Scope}/variants/{variant.Kind.ToString().ToLowerInvariant()}"),
                    cancellationToken);
                uploadedKeys.Add(reference.StorageKey);

                variants.Add(new MediaAssetVariant(
                    asset.Id,
                    variant.Kind,
                    reference.StorageKey,
                    reference.Url,
                    variant.ContentType,
                    variant.Content.Length,
                    variant.Width,
                    variant.Height,
                    now));
            }

            var detailVariant = variants.Single(variant => variant.Kind == MediaAssetVariantKind.Detail);
            return new ProductMediaUploadResult(asset, variants, detailVariant);
        }
        catch
        {
            foreach (var key in uploadedKeys)
            {
                try
                {
                    await storageProvider.DeleteAsync(key, cancellationToken);
                }
                catch
                {
                    // Best-effort cleanup; the upload failure is the actionable error.
                }
            }

            throw;
        }
    }

    public async Task DeleteAsync(
        MediaAsset asset,
        IReadOnlyCollection<MediaAssetVariant> variants,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        asset.RequestDeletion(now);
        var keys = variants
            .Select(variant => variant.StorageKey)
            .Append(asset.StorageKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            foreach (var key in keys)
            {
                await storageProvider.DeleteAsync(key, cancellationToken);
            }

            asset.MarkDeleted(now);
        }
        catch (Exception exception)
        {
            asset.MarkDeleteFailed(exception.Message, now);
        }
    }

    private async Task<byte[]> ReadContentAsync(
        Stream content,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (declaredLength <= 0)
        {
            throw new ArgumentException("Image file cannot be empty.", nameof(declaredLength));
        }

        if (declaredLength > options.MaxFileBytes)
        {
            throw new ArgumentException($"Image file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(declaredLength));
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0)
        {
            throw new ArgumentException("Image file cannot be empty.", nameof(content));
        }

        if (buffer.Length > options.MaxFileBytes)
        {
            throw new ArgumentException($"Image file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(content));
        }

        return buffer.ToArray();
    }

    private string ResolveBucketName() =>
        string.Equals(options.ProviderName, "S3", StringComparison.OrdinalIgnoreCase)
            ? options.S3.BucketName
            : "local";

    private static string NormalizeProviderName(string providerName) =>
        string.IsNullOrWhiteSpace(providerName) ? "Local" : providerName.Trim();
}
