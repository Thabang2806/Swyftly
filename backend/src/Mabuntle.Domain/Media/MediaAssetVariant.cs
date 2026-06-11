using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Media;

public sealed class MediaAssetVariant : Entity
{
    private MediaAssetVariant()
    {
    }

    public MediaAssetVariant(
        Guid mediaAssetId,
        MediaAssetVariantKind kind,
        string storageKey,
        string publicUrl,
        string contentType,
        long byteSize,
        int width,
        int height,
        DateTimeOffset createdAtUtc)
    {
        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("Media asset id is required.", nameof(mediaAssetId));
        }

        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size must be positive.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        MediaAssetId = mediaAssetId;
        Kind = kind;
        StorageKey = Required(storageKey, nameof(storageKey), 700);
        PublicUrl = Required(publicUrl, nameof(publicUrl), 2048);
        ContentType = Required(contentType, nameof(contentType), 100);
        ByteSize = byteSize;
        Width = width;
        Height = height;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid MediaAssetId { get; private set; }

    public MediaAssetVariantKind Kind { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string PublicUrl { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long ByteSize { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

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
}
