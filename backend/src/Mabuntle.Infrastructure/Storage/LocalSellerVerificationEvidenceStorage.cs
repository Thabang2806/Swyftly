using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Media;
using Mabuntle.Application.Sellers;

namespace Mabuntle.Infrastructure.Storage;

public sealed class LocalSellerVerificationEvidenceStorage(
    IOptions<SellerVerificationEvidenceOptions> options,
    IMediaMalwareScanner malwareScanner) : ISellerVerificationEvidenceStorage
{
    private static readonly Dictionary<string, string> ExtensionsByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly SellerVerificationEvidenceOptions options = options.Value;

    public async Task<SellerVerificationEvidenceStoredFile> StoreAsync(
        SellerVerificationEvidenceStorageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Content is null)
        {
            throw new ArgumentException("Evidence file is required.", nameof(request));
        }

        if (request.SellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(request));
        }

        var contentType = NormalizeContentType(request.ContentType);
        if (!ExtensionsByContentType.ContainsKey(contentType)
            || !options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Evidence must be a PDF, JPEG, PNG, or WebP file.", nameof(request));
        }

        var bytes = await ReadContentAsync(request.Content, request.Length, cancellationToken);
        if (!HasExpectedSignature(bytes, contentType))
        {
            throw new ArgumentException("Evidence file signature does not match its content type.", nameof(request));
        }

        var scan = await malwareScanner.ScanAsync(
            new MediaScanRequest(request.FileName, contentType, bytes.Length, bytes),
            cancellationToken);
        if (!scan.IsClean)
        {
            throw new InvalidOperationException(scan.Reason ?? "Evidence file failed malware scanning.");
        }

        var sha256Hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var storageKey = $"{request.SellerId:N}/{Guid.NewGuid():N}{ExtensionsByContentType[contentType]}";
        var absolutePath = ResolvePath(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllBytesAsync(absolutePath, bytes, cancellationToken);

        return new SellerVerificationEvidenceStoredFile(
            "Local",
            storageKey,
            NormalizeFileName(request.FileName, contentType),
            contentType,
            bytes.Length,
            sha256Hash);
    }

    public Task<SellerVerificationEvidenceReadFile?> OpenReadAsync(
        string storageKey,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolvePath(storageKey);
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<SellerVerificationEvidenceReadFile?>(null);
        }

        var stream = File.OpenRead(absolutePath);
        return Task.FromResult<SellerVerificationEvidenceReadFile?>(
            new SellerVerificationEvidenceReadFile(stream, contentType, fileName, stream.Length));
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolvePath(storageKey);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private async Task<byte[]> ReadContentAsync(
        Stream content,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (declaredLength <= 0)
        {
            throw new ArgumentException("Evidence file cannot be empty.", nameof(content));
        }

        if (declaredLength > options.MaxFileBytes)
        {
            throw new ArgumentException($"Evidence file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(content));
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0)
        {
            throw new ArgumentException("Evidence file cannot be empty.", nameof(content));
        }

        if (buffer.Length > options.MaxFileBytes)
        {
            throw new ArgumentException($"Evidence file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(content));
        }

        return buffer.ToArray();
    }

    private string ResolvePath(string storageKey)
    {
        var normalizedKey = storageKey
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (normalizedKey.Length == 0 || normalizedKey.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("Evidence storage path is invalid.");
        }

        var root = GetRootPath();
        var absolutePath = Path.GetFullPath(Path.Combine(root, Path.Combine(normalizedKey)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Evidence storage path is invalid.");
        }

        return absolutePath;
    }

    private string GetRootPath()
    {
        var root = options.LocalRootPath;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(Directory.GetCurrentDirectory(), root);
        }

        return Path.GetFullPath(root);
    }

    private static string NormalizeContentType(string contentType)
    {
        var trimmed = contentType.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "image/jpg" => "image/jpeg",
            _ => trimmed
        };
    }

    private static string NormalizeFileName(string fileName, string contentType)
    {
        var fallback = $"evidence{ExtensionsByContentType[contentType]}";
        var name = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? fallback : fileName.Trim());
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static bool HasExpectedSignature(byte[] bytes, string contentType) =>
        contentType switch
        {
            "application/pdf" => bytes.Length >= 5
                && bytes[0] == 0x25
                && bytes[1] == 0x50
                && bytes[2] == 0x44
                && bytes[3] == 0x46
                && bytes[4] == 0x2D,
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8
                && bytes[0] == 0x89
                && bytes[1] == 0x50
                && bytes[2] == 0x4E
                && bytes[3] == 0x47
                && bytes[4] == 0x0D
                && bytes[5] == 0x0A
                && bytes[6] == 0x1A
                && bytes[7] == 0x0A,
            "image/webp" => bytes.Length >= 12
                && bytes[0] == 0x52
                && bytes[1] == 0x49
                && bytes[2] == 0x46
                && bytes[3] == 0x46
                && bytes[8] == 0x57
                && bytes[9] == 0x45
                && bytes[10] == 0x42
                && bytes[11] == 0x50,
            _ => false
        };
}
