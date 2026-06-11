using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Abstractions;

namespace Mabuntle.Infrastructure.Storage;

public sealed class S3ImageStorageProvider(IOptions<ImageStorageOptions> options) : IImageStorageProvider
{
    private static readonly HttpClient HttpClient = new();
    private readonly ImageStorageOptions options = options.Value;

    public Task<ImageStorageReference> CreateReferenceAsync(
        CreateImageReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var storageKey = NormalizeStorageKey(request.StorageKey);
        var url = string.IsNullOrWhiteSpace(request.Url)
            ? BuildPublicUrl(storageKey)
            : request.Url.Trim();

        return Task.FromResult(new ImageStorageReference(storageKey, url));
    }

    public async Task<ImageStorageReference> UploadAsync(
        UploadImageStorageRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

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
        if (!options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image content type is not allowed.", nameof(request));
        }

        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var storageKey = BuildGeneratedStorageKey(request.Scope, contentType);
        var requestUri = BuildObjectUri(storageKey);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new ByteArrayContent(bytes)
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        if (!string.IsNullOrWhiteSpace(options.S3.CacheControl))
        {
            httpRequest.Headers.TryAddWithoutValidation("Cache-Control", options.S3.CacheControl);
        }

        Sign(httpRequest, bytes);
        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"S3 upload failed with status {(int)response.StatusCode}.");
        }

        return new ImageStorageReference(storageKey, BuildPublicUrl(storageKey));
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var normalizedKey = NormalizeStorageKey(storageKey);
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildObjectUri(normalizedKey));
        Sign(request, []);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"S3 delete failed with status {(int)response.StatusCode}.");
        }
    }

    public Task<ImageStorageReadinessResult> CheckReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var failures = ValidateOptions();
        var result = failures.Count == 0
            ? ImageStorageReadinessResult.Ready("S3-compatible image storage configuration is present.")
            : ImageStorageReadinessResult.NotReady("S3-compatible image storage configuration is incomplete.", failures);

        return Task.FromResult(result);
    }

    private void EnsureConfigured()
    {
        var failures = ValidateOptions();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("S3 image storage configuration is incomplete.");
        }
    }

    private Dictionary<string, string> ValidateOptions()
    {
        var failures = new Dictionary<string, string>();
        RequireValue(options.S3.BucketName, "ImageStorage:S3:BucketName", failures);
        RequireValue(options.S3.Region, "ImageStorage:S3:Region", failures);
        RequireValue(options.S3.AccessKeyId, "ImageStorage:S3:AccessKeyId", failures);
        RequireValue(options.S3.SecretAccessKey, "ImageStorage:S3:SecretAccessKey", failures);
        RequireAbsoluteUrl(options.S3.ServiceUrl, "ImageStorage:S3:ServiceUrl", failures);
        RequireAbsoluteUrl(options.S3.PublicBaseUrl, "ImageStorage:S3:PublicBaseUrl", failures);
        return failures;
    }

    private Uri BuildObjectUri(string storageKey)
    {
        var serviceUri = new Uri(options.S3.ServiceUrl.TrimEnd('/'), UriKind.Absolute);
        var escapedKey = EscapeStorageKey(storageKey);
        if (options.S3.ForcePathStyle)
        {
            var builder = new UriBuilder(serviceUri)
            {
                Path = $"{serviceUri.AbsolutePath.TrimEnd('/')}/{Uri.EscapeDataString(options.S3.BucketName)}/{escapedKey}".TrimStart('/')
            };
            return builder.Uri;
        }

        var virtualHost = $"{options.S3.BucketName}.{serviceUri.Host}";
        var virtualBuilder = new UriBuilder(serviceUri)
        {
            Host = virtualHost,
            Path = $"{serviceUri.AbsolutePath.TrimEnd('/')}/{escapedKey}".TrimStart('/')
        };
        return virtualBuilder.Uri;
    }

    private string BuildPublicUrl(string storageKey) =>
        $"{options.S3.PublicBaseUrl.TrimEnd('/')}/{EscapeStorageKey(storageKey)}";

    private string BuildGeneratedStorageKey(string scope, string contentType)
    {
        var extension = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        var prefix = SanitizeScope(options.S3.KeyPrefix);
        var sanitizedScope = SanitizeScope(scope);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        return string.Join('/', new[] { prefix, sanitizedScope, fileName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void Sign(HttpRequestMessage request, byte[] payload)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = ToHex(SHA256.HashData(payload));
        var host = request.RequestUri!.Host;
        if (!request.RequestUri.IsDefaultPort)
        {
            host = $"{host}:{request.RequestUri.Port}";
        }

        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var canonicalUri = string.IsNullOrWhiteSpace(request.RequestUri.AbsolutePath)
            ? "/"
            : request.RequestUri.AbsolutePath;
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = string.Join('\n',
            request.Method.Method,
            canonicalUri,
            request.RequestUri.Query.TrimStart('?'),
            canonicalHeaders,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{options.S3.Region}/s3/aws4_request";
        var stringToSign = string.Join('\n',
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = GetSignatureKey(options.S3.SecretAccessKey, dateStamp, options.S3.Region, "s3");
        var signature = ToHex(Hmac(signingKey, stringToSign));
        var authorization =
            $"AWS4-HMAC-SHA256 Credential={options.S3.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = Hmac(Encoding.UTF8.GetBytes($"AWS4{key}"), dateStamp);
        var kRegion = Hmac(kDate, regionName);
        var kService = Hmac(kRegion, serviceName);
        return Hmac(kService, "aws4_request");
    }

    private static byte[] Hmac(byte[] key, string data) =>
        new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

    private static string NormalizeStorageKey(string storageKey)
    {
        var segments = storageKey.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Storage key cannot contain path traversal segments.", nameof(storageKey));
        }

        var normalized = string.Join('/', segments);

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key is invalid.", nameof(storageKey));
        }

        return normalized;
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
        string.Join('/', NormalizeStorageKey(storageKey)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static void RequireValue(string value, string key, IDictionary<string, string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures[key] = "missing";
        }
    }

    private static void RequireAbsoluteUrl(string value, string key, IDictionary<string, string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            failures[key] = "missing-or-invalid";
        }
    }

    private static string ToHex(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();
}
