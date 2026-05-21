namespace Swyftly.Infrastructure.Storage;

public sealed class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";

    public string ProviderName { get; set; } = "Local";

    public string LocalRootPath { get; set; } = "storage/product-images";

    public string PublicBasePath { get; set; } = "/media/product-images";

    public long MaxFileBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxPixelWidth { get; set; } = 8000;

    public int MaxPixelHeight { get; set; } = 8000;

    public int VariantWebpQuality { get; set; } = 82;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public S3ImageStorageOptions S3 { get; set; } = new();
}

public sealed class S3ImageStorageOptions
{
    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "af-south-1";

    public string ServiceUrl { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "product-images";

    public bool ForcePathStyle { get; set; } = true;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string CacheControl { get; set; } = "public, max-age=31536000, immutable";
}
