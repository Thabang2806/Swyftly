using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Media;

public sealed class MediaAsset : Entity
{
    private MediaAsset()
    {
    }

    public MediaAsset(
        Guid sellerId,
        Guid productId,
        Guid? productListingRevisionId,
        string provider,
        string bucket,
        string storageKey,
        string publicUrl,
        string originalFileName,
        string contentType,
        long byteSize,
        string sha256Hash,
        int width,
        int height,
        MediaScanStatus scanStatus,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? scannedAtUtc = null)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (productListingRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Revision id cannot be empty.", nameof(productListingRevisionId));
        }

        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size must be positive.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        SellerId = sellerId;
        ProductId = productId;
        ProductListingRevisionId = productListingRevisionId;
        Provider = Required(provider, nameof(provider), 64);
        Bucket = Required(bucket, nameof(bucket), 255);
        StorageKey = Required(storageKey, nameof(storageKey), 700);
        PublicUrl = Required(publicUrl, nameof(publicUrl), 2048);
        OriginalFileName = Required(originalFileName, nameof(originalFileName), 255);
        ContentType = Required(contentType, nameof(contentType), 100);
        ByteSize = byteSize;
        Sha256Hash = Required(sha256Hash, nameof(sha256Hash), 128);
        Width = width;
        Height = height;
        ScanStatus = scanStatus;
        LifecycleStatus = MediaAssetLifecycleStatus.Stored;
        CreatedAtUtc = createdAtUtc;
        ScannedAtUtc = scannedAtUtc;
    }

    public Guid SellerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? ProductListingRevisionId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Bucket { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string PublicUrl { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long ByteSize { get; private set; }

    public string Sha256Hash { get; private set; } = string.Empty;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public MediaScanStatus ScanStatus { get; private set; }

    public MediaAssetLifecycleStatus LifecycleStatus { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ScannedAtUtc { get; private set; }

    public DateTimeOffset? DeleteRequestedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public void MarkClean(DateTimeOffset scannedAtUtc)
    {
        ScanStatus = MediaScanStatus.Clean;
        ScannedAtUtc = scannedAtUtc;
        LastError = null;
    }

    public void MarkRejected(string reason, DateTimeOffset scannedAtUtc)
    {
        ScanStatus = MediaScanStatus.Rejected;
        ScannedAtUtc = scannedAtUtc;
        LastError = TrimOrNull(reason, 500);
    }

    public void RequestDeletion(DateTimeOffset now)
    {
        if (LifecycleStatus == MediaAssetLifecycleStatus.Deleted)
        {
            return;
        }

        LifecycleStatus = MediaAssetLifecycleStatus.PendingDeletion;
        DeleteRequestedAtUtc ??= now;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        LifecycleStatus = MediaAssetLifecycleStatus.Deleted;
        DeletedAtUtc = now;
        LastError = null;
    }

    public void MarkDeleteFailed(string error, DateTimeOffset now)
    {
        LifecycleStatus = MediaAssetLifecycleStatus.DeleteFailed;
        DeleteRequestedAtUtc ??= now;
        LastError = TrimOrNull(error, 500);
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
