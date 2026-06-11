using System.Text.Json;
using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class ProductListingRevision : AuditableEntity
{
    private ProductListingRevision()
    {
    }

    public ProductListingRevision(
        Guid productId,
        Guid sellerId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        ProductId = productId;
        SellerId = sellerId;
        Status = ProductListingRevisionStatus.Draft;
    }

    public Guid ProductId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public string? Title { get; private set; }

    public string? Slug { get; private set; }

    public string? ShortDescription { get; private set; }

    public string? FullDescription { get; private set; }

    public string TagsJson { get; private set; } = "[]";

    public string AttributesJson { get; private set; } = "{}";

    public string? SeoTitle { get; private set; }

    public string? SeoDescription { get; private set; }

    public string? MerchandisingLabel { get; private set; }

    public string? CareInstructions { get; private set; }

    public string? ProductDisclaimer { get; private set; }

    public ProductListingRevisionStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public bool CanSellerEdit => Status is ProductListingRevisionStatus.Draft or ProductListingRevisionStatus.Rejected;

    public void UpdateProposal(
        Guid? categoryId,
        Guid? brandId,
        string? title,
        string? slug,
        string? shortDescription,
        string? fullDescription,
        string tagsJson,
        string attributesJson,
        string? seoTitle = null,
        string? seoDescription = null,
        string? merchandisingLabel = null,
        string? careInstructions = null,
        string? productDisclaimer = null)
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

        EnsureJsonArray(tagsJson, nameof(tagsJson));
        EnsureJsonObject(attributesJson, nameof(attributesJson));

        CategoryId = categoryId;
        BrandId = brandId;
        Title = TrimOrNull(title);
        Slug = NormalizeSlugOrNull(slug);
        ShortDescription = TrimOrNull(shortDescription);
        FullDescription = TrimOrNull(fullDescription);
        TagsJson = tagsJson;
        AttributesJson = attributesJson;
        SeoTitle = Optional(seoTitle, nameof(seoTitle), Product.SeoTitleMaxLength);
        SeoDescription = Optional(seoDescription, nameof(seoDescription), Product.SeoDescriptionMaxLength);
        MerchandisingLabel = Optional(merchandisingLabel, nameof(merchandisingLabel), Product.MerchandisingLabelMaxLength);
        CareInstructions = Optional(careInstructions, nameof(careInstructions), Product.CareInstructionsMaxLength);
        ProductDisclaimer = Optional(productDisclaimer, nameof(productDisclaimer), Product.ProductDisclaimerMaxLength);

        if (Status == ProductListingRevisionStatus.Rejected)
        {
            Status = ProductListingRevisionStatus.Draft;
            RejectionReason = null;
            ReviewedByUserId = null;
            ReviewedAtUtc = null;
        }
    }

    public void SubmitForReview(
        bool hasAtLeastOneImage,
        DateTimeOffset submittedAtUtc)
    {
        EnsureSellerEditable();

        if (!CategoryId.HasValue ||
            !HasValue(Title) ||
            !HasValue(Slug) ||
            !HasValue(ShortDescription) ||
            !HasValue(FullDescription) ||
            !hasAtLeastOneImage)
        {
            throw new InvalidOperationException("Revision requires category, title, slug, descriptions, and at least one image before review submission.");
        }

        Status = ProductListingRevisionStatus.PendingReview;
        RejectionReason = null;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void Approve(
        Guid reviewedByUserId,
        DateTimeOffset reviewedAtUtc)
    {
        if (Status != ProductListingRevisionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending revisions can be approved.");
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        }

        Status = ProductListingRevisionStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        RejectionReason = null;
    }

    public void Reject(
        string reason,
        Guid reviewedByUserId,
        DateTimeOffset reviewedAtUtc)
    {
        if (Status != ProductListingRevisionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only pending revisions can be rejected.");
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        }

        Status = ProductListingRevisionStatus.Rejected;
        RejectionReason = Required(reason, nameof(reason));
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public void Cancel()
    {
        if (Status is ProductListingRevisionStatus.Approved or ProductListingRevisionStatus.Cancelled)
        {
            return;
        }

        Status = ProductListingRevisionStatus.Cancelled;
    }

    private void EnsureSellerEditable()
    {
        if (!CanSellerEdit)
        {
            throw new InvalidOperationException("Only draft or rejected revisions can be edited.");
        }
    }

    private static void EnsureJsonArray(string json, string parameterName)
    {
        using var document = JsonDocument.Parse(Required(json, parameterName));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Value must be a JSON array.", parameterName);
        }
    }

    private static void EnsureJsonObject(string json, string parameterName)
    {
        using var document = JsonDocument.Parse(Required(json, parameterName));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Value must be a JSON object.", parameterName);
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

    private static string? Optional(string? value, string parameterName, int maxLength)
    {
        var trimmed = TrimOrNull(value);
        if (trimmed is not null && trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
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
