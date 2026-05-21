using Swyftly.Domain.Common;

namespace Swyftly.Domain.Catalog;

public sealed class Product : AuditableEntity
{
    private Product()
    {
    }

    public Product(Guid sellerId)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        Status = ProductStatus.Draft;
    }

    public Guid SellerId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public string? Title { get; private set; }

    public string? Slug { get; private set; }

    public string? ShortDescription { get; private set; }

    public string? FullDescription { get; private set; }

    public string TagsJson { get; private set; } = "[]";

    public ProductStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public bool CanSellerEdit => Status is ProductStatus.Draft or ProductStatus.Rejected or ProductStatus.ChangesRequested;

    public void UpdateDraftDetails(
        Guid? categoryId,
        Guid? brandId,
        string? title,
        string? slug,
        string? shortDescription,
        string? fullDescription)
    {
        EnsureSellerEditable();

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category id cannot be empty.", nameof(categoryId));
        }

        if (brandId == Guid.Empty)
        {
            throw new ArgumentException("Brand id cannot be empty.", nameof(brandId));
        }

        CategoryId = categoryId;
        BrandId = brandId;
        Title = TrimOrNull(title);
        Slug = NormalizeSlugOrNull(slug);
        ShortDescription = TrimOrNull(shortDescription);
        FullDescription = TrimOrNull(fullDescription);
    }

    public void UpdateTags(string tagsJson)
    {
        EnsureSellerEditable();
        TagsJson = Required(tagsJson, nameof(tagsJson));
    }

    public bool CanSubmitForReview(bool hasAtLeastOneImage, bool hasAtLeastOneActiveVariant)
    {
        return CanSellerEdit
            && CategoryId.HasValue
            && HasValue(Title)
            && HasValue(Slug)
            && HasValue(ShortDescription)
            && HasValue(FullDescription)
            && hasAtLeastOneImage
            && hasAtLeastOneActiveVariant;
    }

    public void SubmitForReview(
        bool hasAtLeastOneImage,
        bool hasAtLeastOneActiveVariant,
        bool needsAdminReview = false)
    {
        if (!CanSubmitForReview(hasAtLeastOneImage, hasAtLeastOneActiveVariant))
        {
            throw new InvalidOperationException("Product requires category, title, slug, descriptions, at least one image, and at least one active variant before review submission.");
        }

        Status = needsAdminReview
            ? ProductStatus.NeedsAdminReview
            : ProductStatus.PendingReview;
        RejectionReason = null;
    }

    public void Publish(DateTimeOffset publishedAtUtc)
    {
        if (Status is not (ProductStatus.PendingReview or ProductStatus.NeedsAdminReview))
        {
            throw new InvalidOperationException("Only products pending review or needing admin review can be published.");
        }

        Status = ProductStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Status is not (ProductStatus.PendingReview or ProductStatus.NeedsAdminReview))
        {
            throw new InvalidOperationException("Only products pending review or needing admin review can be rejected.");
        }

        RejectionReason = Required(reason, nameof(reason));
        Status = ProductStatus.Rejected;
    }

    public void RequestChanges(string reason)
    {
        if (Status is not (ProductStatus.PendingReview or ProductStatus.NeedsAdminReview))
        {
            throw new InvalidOperationException("Only products pending review or needing admin review can have changes requested.");
        }

        RejectionReason = Required(reason, nameof(reason));
        Status = ProductStatus.ChangesRequested;
    }

    public void Archive()
    {
        if (Status == ProductStatus.Archived)
        {
            return;
        }

        Status = ProductStatus.Archived;
    }

    public void MarkOutOfStock()
    {
        if (Status != ProductStatus.Published)
        {
            throw new InvalidOperationException("Only published products can be marked out of stock.");
        }

        Status = ProductStatus.OutOfStock;
    }

    public void Restock()
    {
        if (Status != ProductStatus.OutOfStock)
        {
            throw new InvalidOperationException("Only out-of-stock products can be restored to published.");
        }

        Status = ProductStatus.Published;
    }

    public void ApplyApprovedListingRevision(
        Guid? categoryId,
        Guid? brandId,
        string? title,
        string? slug,
        string? shortDescription,
        string? fullDescription,
        string tagsJson)
    {
        if (Status != ProductStatus.Published)
        {
            throw new InvalidOperationException("Only published products can apply approved listing revisions.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category id cannot be empty.", nameof(categoryId));
        }

        if (brandId == Guid.Empty)
        {
            throw new ArgumentException("Brand id cannot be empty.", nameof(brandId));
        }

        CategoryId = categoryId;
        BrandId = brandId;
        Title = TrimOrNull(title);
        Slug = NormalizeSlugOrNull(slug);
        ShortDescription = TrimOrNull(shortDescription);
        FullDescription = TrimOrNull(fullDescription);
        TagsJson = Required(tagsJson, nameof(tagsJson));
    }

    private void EnsureSellerEditable()
    {
        if (!CanSellerEdit)
        {
            throw new InvalidOperationException("Seller can edit only draft, rejected, or changes-requested products.");
        }
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

    private static string? NormalizeSlugOrNull(string? slug)
    {
        var normalized = TrimOrNull(slug)?.ToLowerInvariant();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Slug can only contain letters, numbers, and hyphens.", nameof(slug));
        }

        return normalized;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
