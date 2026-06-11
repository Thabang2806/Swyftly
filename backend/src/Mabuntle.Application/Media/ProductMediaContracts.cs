using Mabuntle.Domain.Media;

namespace Mabuntle.Application.Media;

public interface IMediaMalwareScanner
{
    Task<MediaScanResult> ScanAsync(
        MediaScanRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProductMediaUploadService
{
    Task<ProductMediaUploadResult> UploadAsync(
        ProductMediaUploadRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        MediaAsset asset,
        IReadOnlyCollection<MediaAssetVariant> variants,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IMediaCleanupService
{
    Task<MediaCleanupResult> CleanupAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record MediaScanRequest(
    string FileName,
    string ContentType,
    long Length,
    byte[] Content);

public sealed record MediaScanResult(
    bool IsClean,
    string ProviderName,
    string? Reason)
{
    public static MediaScanResult Clean(string providerName) => new(true, providerName, null);

    public static MediaScanResult Rejected(string providerName, string reason) => new(false, providerName, reason);
}

public sealed record ProductMediaUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string Scope,
    Guid SellerId,
    Guid ProductId,
    Guid? ProductListingRevisionId);

public sealed record ProductMediaUploadResult(
    MediaAsset Asset,
    IReadOnlyList<MediaAssetVariant> Variants,
    MediaAssetVariant DetailVariant);

public sealed record MediaCleanupResult(
    int ProcessedCount,
    int DeletedCount,
    int FailedCount);
