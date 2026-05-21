using Swyftly.Domain.Common;

namespace Swyftly.Domain.Catalog;

public sealed class ProductListingRevisionImage : Entity
{
    private ProductListingRevisionImage()
    {
    }

    public ProductListingRevisionImage(
        Guid revisionId,
        Guid? sourceProductImageId,
        string url,
        string storageKey,
        string? altText,
        int sortOrder,
        bool isPrimary,
        DateTimeOffset createdAtUtc,
        Guid? mediaAssetId = null)
    {
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("Revision id is required.", nameof(revisionId));
        }

        if (sourceProductImageId == Guid.Empty)
        {
            throw new ArgumentException("Source product image id cannot be empty.", nameof(sourceProductImageId));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("Media asset id cannot be empty.", nameof(mediaAssetId));
        }

        RevisionId = revisionId;
        SourceProductImageId = sourceProductImageId;
        MediaAssetId = mediaAssetId;
        Url = Required(url, nameof(url));
        StorageKey = Required(storageKey, nameof(storageKey));
        AltText = TrimOrNull(altText);
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid RevisionId { get; private set; }

    public Guid? SourceProductImageId { get; private set; }

    public Guid? MediaAssetId { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string? AltText { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void MarkPrimary() => IsPrimary = true;

    public void ClearPrimary() => IsPrimary = false;

    public void UpdateMetadata(string? altText, int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        AltText = TrimOrNull(altText);
        SortOrder = sortOrder;
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
}
