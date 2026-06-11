using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerFunnelEvent : Entity
{
    public const int HashedVisitorIdMaxLength = 64;
    public const int SourceRouteMaxLength = 300;
    public const int IdempotencyKeyMaxLength = 200;
    public const int UtmSourceMaxLength = 100;
    public const int UtmMediumMaxLength = 100;
    public const int UtmCampaignMaxLength = 150;
    public const int ReferrerHostMaxLength = 150;
    public const int SourceCategoryMaxLength = 40;

    private SellerFunnelEvent()
    {
    }

    public SellerFunnelEvent(
        Guid sellerId,
        SellerFunnelEventType eventType,
        DateTimeOffset occurredAtUtc,
        Guid? productId = null,
        Guid? cartId = null,
        Guid? orderId = null,
        Guid? buyerId = null,
        string? hashedAnonymousVisitorId = null,
        string? sourceRoute = null,
        string? idempotencyKey = null,
        string? utmSource = null,
        string? utmMedium = null,
        string? utmCampaign = null,
        string? referrerHost = null,
        string? sourceCategory = null)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        SellerId = sellerId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        ProductId = productId == Guid.Empty ? null : productId;
        CartId = cartId == Guid.Empty ? null : cartId;
        OrderId = orderId == Guid.Empty ? null : orderId;
        BuyerId = buyerId == Guid.Empty ? null : buyerId;
        HashedAnonymousVisitorId = TrimOrNull(hashedAnonymousVisitorId, HashedVisitorIdMaxLength);
        SourceRoute = TrimOrNull(sourceRoute, SourceRouteMaxLength);
        IdempotencyKey = TrimOrNull(idempotencyKey, IdempotencyKeyMaxLength);
        UtmSource = TrimOrNull(utmSource, UtmSourceMaxLength);
        UtmMedium = TrimOrNull(utmMedium, UtmMediumMaxLength);
        UtmCampaign = TrimOrNull(utmCampaign, UtmCampaignMaxLength);
        ReferrerHost = TrimOrNull(referrerHost, ReferrerHostMaxLength)?.ToLowerInvariant();
        SourceCategory = TrimOrNull(sourceCategory, SourceCategoryMaxLength);
    }

    public Guid SellerId { get; private set; }

    public Guid? ProductId { get; private set; }

    public Guid? CartId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? BuyerId { get; private set; }

    public string? HashedAnonymousVisitorId { get; private set; }

    public SellerFunnelEventType EventType { get; private set; }

    public string? SourceRoute { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string? UtmSource { get; private set; }

    public string? UtmMedium { get; private set; }

    public string? UtmCampaign { get; private set; }

    public string? ReferrerHost { get; private set; }

    public string? SourceCategory { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

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
