using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerStorefront : AuditableEntity
{
    private SellerStorefront()
    {
    }

    public SellerStorefront(
        Guid sellerId,
        string storeName,
        string slug,
        string? description = null,
        string? logoUrl = null,
        string? bannerUrl = null)
    {
        SellerId = sellerId;
        StoreName = Required(storeName, nameof(storeName));
        Slug = NormalizeSlug(slug);
        Description = TrimOrNull(description);
        LogoUrl = TrimOrNull(logoUrl);
        BannerUrl = TrimOrNull(bannerUrl);
        IsPublished = false;
    }

    public Guid SellerId { get; private set; }

    public string StoreName { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? BannerUrl { get; private set; }

    public bool IsPublished { get; private set; }

    public bool HasRequiredFields() => HasValue(StoreName) && HasValue(Slug);

    public void Update(
        string storeName,
        string slug,
        string? description,
        string? logoUrl,
        string? bannerUrl)
    {
        StoreName = Required(storeName, nameof(storeName));
        Slug = NormalizeSlug(slug);
        Description = TrimOrNull(description);
        LogoUrl = TrimOrNull(logoUrl);
        BannerUrl = TrimOrNull(bannerUrl);
    }

    public void Publish()
    {
        if (!HasRequiredFields())
        {
            throw new InvalidOperationException("Storefront must have a store name and slug before publishing.");
        }

        IsPublished = true;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = Required(slug, nameof(slug)).ToLowerInvariant();

        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Slug can only contain letters, numbers, and hyphens.", nameof(slug));
        }

        return normalized;
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

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
