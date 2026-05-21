using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Swyftly.Domain.Media;

namespace Swyftly.Infrastructure.Storage;

public sealed class MediaImageProcessor(IOptions<ImageStorageOptions> options)
{
    private readonly ImageStorageOptions options = options.Value;

    public ProcessedMediaImage Process(byte[] bytes, string declaredContentType)
    {
        if (bytes.Length <= 0)
        {
            throw new ArgumentException("Image file cannot be empty.", nameof(bytes));
        }

        if (bytes.Length > options.MaxFileBytes)
        {
            throw new ArgumentException($"Image file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(bytes));
        }

        using var codecStream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(codecStream)
            ?? throw new ArgumentException("Image file could not be decoded.", nameof(bytes));

        var contentType = ToContentType(codec.EncodedFormat);
        if (!options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image content type is not allowed.", nameof(bytes));
        }

        if (!string.Equals(NormalizeContentType(declaredContentType), contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image file signature does not match its content type.", nameof(declaredContentType));
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
        {
            throw new ArgumentException("Image dimensions are invalid.", nameof(bytes));
        }

        if (info.Width > options.MaxPixelWidth || info.Height > options.MaxPixelHeight)
        {
            throw new ArgumentException($"Image dimensions cannot exceed {options.MaxPixelWidth}x{options.MaxPixelHeight} pixels.", nameof(bytes));
        }

        using var bitmap = SKBitmap.Decode(bytes)
            ?? throw new ArgumentException("Image file could not be decoded.", nameof(bytes));

        var variants = new[]
        {
            CreateVariant(bitmap, MediaAssetVariantKind.Thumb, 320),
            CreateVariant(bitmap, MediaAssetVariantKind.Card, 640),
            CreateVariant(bitmap, MediaAssetVariantKind.Detail, 1600)
        };

        return new ProcessedMediaImage(
            contentType,
            info.Width,
            info.Height,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            variants);
    }

    private ProcessedMediaVariant CreateVariant(SKBitmap source, MediaAssetVariantKind kind, int maxSide)
    {
        var scale = Math.Min(1d, maxSide / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = scale >= 1d
            ? source.Copy()
            : source.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), SKFilterQuality.High);

        if (resized is null)
        {
            throw new InvalidOperationException("Image variant generation failed.");
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, Math.Clamp(options.VariantWebpQuality, 1, 100))
            ?? throw new InvalidOperationException("Image variant encoding failed.");

        return new ProcessedMediaVariant(kind, encoded.ToArray(), width, height, "image/webp");
    }

    private static string NormalizeContentType(string contentType)
    {
        var normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return normalized == "image/jpg" ? "image/jpeg" : normalized;
    }

    private static string ToContentType(SKEncodedImageFormat format) =>
        format switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => throw new ArgumentException("Image must be a JPEG, PNG, or WebP file.", nameof(format))
        };
}

public sealed record ProcessedMediaImage(
    string ContentType,
    int Width,
    int Height,
    string Sha256Hash,
    IReadOnlyList<ProcessedMediaVariant> Variants);

public sealed record ProcessedMediaVariant(
    MediaAssetVariantKind Kind,
    byte[] Content,
    int Width,
    int Height,
    string ContentType);
