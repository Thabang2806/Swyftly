using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class ProductReview : AuditableEntity
{
    private const int MaxTitleLength = 160;
    private const int MaxBodyLength = 2000;

    private ProductReview()
    {
    }

    public ProductReview(
        Guid buyerId,
        Guid sellerId,
        Guid productId,
        Guid orderId,
        Guid orderItemId,
        int rating,
        string? title,
        string? body,
        DateTimeOffset createdAtUtc)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (orderItemId == Guid.Empty)
        {
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));
        }

        BuyerId = buyerId;
        SellerId = sellerId;
        ProductId = productId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        Status = ProductReviewStatus.PendingReview;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        UpdateContent(rating, title, body);
    }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public int Rating { get; private set; }

    public string? Title { get; private set; }

    public string? Body { get; private set; }

    public ProductReviewStatus Status { get; private set; }

    public string? ModerationReason { get; private set; }

    public Guid? ModeratedByUserId { get; private set; }

    public DateTimeOffset? ModeratedAtUtc { get; private set; }

    public bool IsPublic => Status == ProductReviewStatus.Published;

    public void Update(int rating, string? title, string? body, DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        UpdateContent(rating, title, body);
        Status = ProductReviewStatus.PendingReview;
        ModerationReason = null;
        ModeratedByUserId = null;
        ModeratedAtUtc = null;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Approve(Guid moderatedByUserId, DateTimeOffset moderatedAtUtc)
    {
        EnsureModerator(moderatedByUserId);
        EnsureEditable();

        Status = ProductReviewStatus.Published;
        ModerationReason = null;
        ModeratedByUserId = moderatedByUserId;
        ModeratedAtUtc = moderatedAtUtc;
        UpdatedAtUtc = moderatedAtUtc;
    }

    public void Reject(string reason, Guid moderatedByUserId, DateTimeOffset moderatedAtUtc)
    {
        EnsureModerator(moderatedByUserId);
        EnsureEditable();

        Status = ProductReviewStatus.Rejected;
        ModerationReason = RequiredReason(reason);
        ModeratedByUserId = moderatedByUserId;
        ModeratedAtUtc = moderatedAtUtc;
        UpdatedAtUtc = moderatedAtUtc;
    }

    public void Remove(DateTimeOffset removedAtUtc, Guid? moderatedByUserId = null, string? reason = null)
    {
        if (Status == ProductReviewStatus.Removed)
        {
            return;
        }

        if (moderatedByUserId.HasValue)
        {
            EnsureModerator(moderatedByUserId.Value);
            ModeratedByUserId = moderatedByUserId;
            ModeratedAtUtc = removedAtUtc;
            ModerationReason = string.IsNullOrWhiteSpace(reason)
                ? ModerationReason
                : RequiredReason(reason);
        }

        Status = ProductReviewStatus.Removed;
        UpdatedAtUtc = removedAtUtc;
    }

    private void EnsureEditable()
    {
        if (Status == ProductReviewStatus.Removed)
        {
            throw new InvalidOperationException("Removed reviews cannot be updated.");
        }
    }

    private static void EnsureModerator(Guid moderatedByUserId)
    {
        if (moderatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Moderator user id is required.", nameof(moderatedByUserId));
        }
    }

    private static string RequiredReason(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Moderation reason is required.", nameof(value));
        }

        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }

    private void UpdateContent(int rating, string? title, string? body)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        Rating = rating;
        Title = TrimOrNull(title, MaxTitleLength);
        Body = TrimOrNull(body, MaxBodyLength);
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
