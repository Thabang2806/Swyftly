using Microsoft.Extensions.Options;
using Swyftly.Application.Abstractions;

namespace Swyftly.Infrastructure.Storage;

public sealed class LocalImageStorageProvider(
    IOptions<ImageStorageOptions> options) : IImageStorageProvider
{
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ImageStorageOptions options = options.Value;

    public Task<ImageStorageReference> CreateReferenceAsync(
        CreateImageReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var storageKey = Required(request.StorageKey, nameof(request.StorageKey));
        var url = string.IsNullOrWhiteSpace(request.Url)
            ? $"{NormalizeBasePath(options.PublicBasePath)}/{EscapeStorageKey(storageKey)}"
            : request.Url.Trim();

        return Task.FromResult(new ImageStorageReference(storageKey, url));
    }

    public async Task<ImageStorageReference> UploadAsync(
        UploadImageStorageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Content is null)
        {
            throw new ArgumentException("Image content is required.", nameof(request));
        }

        if (request.Length <= 0)
        {
            throw new ArgumentException("Image file cannot be empty.", nameof(request));
        }

        if (request.Length > options.MaxFileBytes)
        {
            throw new ArgumentException($"Image file cannot exceed {options.MaxFileBytes / 1024 / 1024} MB.", nameof(request));
        }

        var contentType = Required(request.ContentType, nameof(request.ContentType)).ToLowerInvariant();
        if (!SupportedContentTypes.Contains(contentType) ||
            !options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image must be a JPEG, PNG, or WebP file.", nameof(request));
        }

        if (!await HasExpectedSignatureAsync(request.Content, contentType, cancellationToken))
        {
            throw new ArgumentException("Image file signature does not match its content type.", nameof(request));
        }

        var extension = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var scope = SanitizeScope(request.Scope);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var storageKey = string.IsNullOrWhiteSpace(scope) ? fileName : $"{scope}/{fileName}";
        var root = GetRootPath();
        var absolutePath = Path.GetFullPath(Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Image storage path is invalid.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using (var fileStream = File.Create(absolutePath))
        {
            await request.Content.CopyToAsync(fileStream, cancellationToken);
        }

        var url = $"{NormalizeBasePath(options.PublicBasePath)}/{EscapeStorageKey(storageKey)}";
        return new ImageStorageReference(storageKey, url);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = Required(storageKey, nameof(storageKey));
        var root = GetRootPath();
        var absolutePath = Path.GetFullPath(Path.Combine(root, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public Task<ImageStorageReadinessResult> CheckReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string>();

        try
        {
            var root = GetRootPath();
            Directory.CreateDirectory(root);
            if (!Directory.Exists(root))
            {
                failures["ImageStorage:LocalRootPath"] = "root-not-found";
            }
        }
        catch (Exception exception)
        {
            failures["ImageStorage:LocalRootPath"] = exception.GetType().Name;
        }

        var result = failures.Count == 0
            ? ImageStorageReadinessResult.Ready("Local image storage is writable.")
            : ImageStorageReadinessResult.NotReady("Local image storage configuration is invalid.", failures);

        return Task.FromResult(result);
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

    private static async Task<bool> HasExpectedSignatureAsync(
        Stream stream,
        string contentType,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        return contentType switch
        {
            "image/jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => bytesRead >= 8
                && header[0] == 0x89
                && header[1] == 0x50
                && header[2] == 0x4E
                && header[3] == 0x47
                && header[4] == 0x0D
                && header[5] == 0x0A
                && header[6] == 0x1A
                && header[7] == 0x0A,
            "image/webp" => bytesRead >= 12
                && header[0] == 0x52
                && header[1] == 0x49
                && header[2] == 0x46
                && header[3] == 0x46
                && header[8] == 0x57
                && header[9] == 0x45
                && header[10] == 0x42
                && header[11] == 0x50,
            _ => false
        };
    }

    private static string SanitizeScope(string scope)
    {
        var segments = scope
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => new string(segment
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                .ToArray()))
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        return string.Join('/', segments);
    }

    private static string EscapeStorageKey(string storageKey) =>
        string.Join('/', storageKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

    private static string NormalizeBasePath(string path)
    {
        var trimmed = string.IsNullOrWhiteSpace(path) ? "/media/product-images" : path.Trim();
        return trimmed.StartsWith('/') ? trimmed.TrimEnd('/') : $"/{trimmed.TrimEnd('/')}";
    }

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
