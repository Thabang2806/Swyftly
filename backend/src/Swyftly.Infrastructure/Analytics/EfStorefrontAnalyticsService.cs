using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Swyftly.Application.Analytics;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Analytics;

public sealed class EfStorefrontAnalyticsService(
    SwyftlyDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<EfStorefrontAnalyticsService> logger) : IStorefrontAnalyticsService
{
    private static readonly TimeSpan ViewDedupeWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ActionDedupeWindow = TimeSpan.FromMinutes(5);
    private static readonly string[] AllowedSourceCategories =
    [
        "Direct",
        "Search",
        "Social",
        "Email",
        "Ads",
        "Referral",
        "Unknown"
    ];

    public async Task<Result<StorefrontFunnelEventResult>> RecordClientEventAsync(
        StorefrontFunnelEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailures = ValidateClientRequest(request);
        if (validationFailures.Count > 0)
        {
            return Result<StorefrontFunnelEventResult>.Failure(Error.Validation(validationFailures));
        }

        var now = timeProvider.GetUtcNow();
        var attribution = NormalizeAttribution(request);
        var target = await ResolveClientEventTargetAsync(request, cancellationToken);
        if (target is null)
        {
            return Result<StorefrontFunnelEventResult>.Failure(
                Error.NotFound("StorefrontAnalytics.TargetNotFound", "The analytics event target was not found or is not publicly trackable."));
        }

        var hashedVisitorId = HashVisitorId(request.AnonymousVisitorId);
        var idempotencyKey = TrimOrNull(request.IdempotencyKey, SellerFunnelEvent.IdempotencyKeyMaxLength);
        if (await IsDuplicateAsync(
            request.EventType,
            target.SellerId,
            target.ProductId,
            target.CartId,
            target.OrderId,
            request.BuyerId,
            hashedVisitorId,
            idempotencyKey,
            now,
            cancellationToken))
        {
            return Result<StorefrontFunnelEventResult>.Success(new StorefrontFunnelEventResult(false, null, "Duplicate"));
        }

        var funnelEvent = new SellerFunnelEvent(
            target.SellerId,
            request.EventType,
            now,
            target.ProductId,
            target.CartId,
            target.OrderId,
            request.BuyerId,
            hashedVisitorId,
            request.SourceRoute,
            idempotencyKey,
            attribution.UtmSource,
            attribution.UtmMedium,
            attribution.UtmCampaign,
            attribution.ReferrerHost,
            attribution.SourceCategory);
        dbContext.SellerFunnelEvents.Add(funnelEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<StorefrontFunnelEventResult>.Success(new StorefrontFunnelEventResult(true, funnelEvent.Id, "Recorded"));
    }

    public Task RecordOrderCreatedAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        RecordOrderEventBestEffortAsync(orderId, SellerFunnelEventType.OrderCreated, cancellationToken);

    public Task RecordOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        RecordOrderEventBestEffortAsync(orderId, SellerFunnelEventType.OrderPaid, cancellationToken);

    private async Task RecordOrderEventBestEffortAsync(
        Guid orderId,
        SellerFunnelEventType eventType,
        CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        try
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
            if (order is null)
            {
                return;
            }

            var idempotencyKey = $"{eventType}:{order.Id:N}";
            if (await dbContext.SellerFunnelEvents.AnyAsync(
                funnelEvent => funnelEvent.EventType == eventType
                    && funnelEvent.SellerId == order.SellerId
                    && funnelEvent.OrderId == order.Id,
                cancellationToken))
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            var attribution = await ResolveOrderAttributionAsync(order, cancellationToken);
            dbContext.SellerFunnelEvents.Add(new SellerFunnelEvent(
                order.SellerId,
                eventType,
                now,
                productId: null,
                cartId: order.CartId,
                orderId: order.Id,
                buyerId: order.BuyerId,
                hashedAnonymousVisitorId: null,
                sourceRoute: "server",
                idempotencyKey: idempotencyKey,
                utmSource: attribution?.UtmSource,
                utmMedium: attribution?.UtmMedium,
                utmCampaign: attribution?.UtmCampaign,
                referrerHost: attribution?.ReferrerHost,
                sourceCategory: attribution?.SourceCategory ?? "Unknown"));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to record seller funnel event {EventType} for order {OrderId}.",
                eventType,
                orderId);
        }
    }

    private async Task<ClientEventTarget?> ResolveClientEventTargetAsync(
        StorefrontFunnelEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EventType is SellerFunnelEventType.ProductViewed or SellerFunnelEventType.ProductAddedToCart)
        {
            if (!request.ProductId.HasValue)
            {
                return null;
            }

            var product = await dbContext.Products
                .AsNoTracking()
                .Where(product => product.Id == request.ProductId.Value
                    && product.Status == ProductStatus.Published
                    && dbContext.SellerProfiles.Any(seller =>
                        seller.Id == product.SellerId
                        && seller.VerificationStatus == SellerVerificationStatus.Verified)
                    && dbContext.SellerStorefronts.Any(storefront =>
                        storefront.SellerId == product.SellerId
                        && storefront.IsPublished))
                .Select(product => new { product.SellerId, ProductId = product.Id })
                .SingleOrDefaultAsync(cancellationToken);

            return product is null
                ? null
                : new ClientEventTarget(product.SellerId, product.ProductId, null, null);
        }

        if (request.EventType == SellerFunnelEventType.StorefrontViewed)
        {
            var slug = request.SellerStoreSlug?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var storefront = await dbContext.SellerStorefronts
                .AsNoTracking()
                .Where(storefront => storefront.Slug == slug
                    && storefront.IsPublished
                    && dbContext.SellerProfiles.Any(seller =>
                        seller.Id == storefront.SellerId
                        && seller.VerificationStatus == SellerVerificationStatus.Verified))
                .Select(storefront => new { storefront.SellerId })
                .SingleOrDefaultAsync(cancellationToken);

            return storefront is null
                ? null
                : new ClientEventTarget(storefront.SellerId, null, null, null);
        }

        if (request.EventType == SellerFunnelEventType.CheckoutStarted)
        {
            if (!request.CartId.HasValue || !request.BuyerId.HasValue)
            {
                return null;
            }

            var cart = await dbContext.Carts
                .AsNoTracking()
                .Where(cart => cart.Id == request.CartId.Value
                    && cart.BuyerId == request.BuyerId.Value
                    && cart.Status == CartStatus.Active
                    && cart.SellerId.HasValue)
                .Select(cart => new { SellerId = cart.SellerId!.Value, CartId = cart.Id })
                .SingleOrDefaultAsync(cancellationToken);

            return cart is null
                ? null
                : new ClientEventTarget(cart.SellerId, null, cart.CartId, null);
        }

        return null;
    }

    private async Task<EventAttribution?> ResolveOrderAttributionAsync(Order order, CancellationToken cancellationToken)
    {
        return await dbContext.SellerFunnelEvents
            .AsNoTracking()
            .Where(funnelEvent => funnelEvent.SellerId == order.SellerId
                && funnelEvent.EventType != SellerFunnelEventType.OrderCreated
                && funnelEvent.EventType != SellerFunnelEventType.OrderPaid
                && ((funnelEvent.CartId.HasValue && funnelEvent.CartId == order.CartId)
                    || (funnelEvent.BuyerId.HasValue && funnelEvent.BuyerId == order.BuyerId)))
            .OrderByDescending(funnelEvent => funnelEvent.OccurredAtUtc)
            .Select(funnelEvent => new EventAttribution(
                funnelEvent.UtmSource,
                funnelEvent.UtmMedium,
                funnelEvent.UtmCampaign,
                funnelEvent.ReferrerHost,
                funnelEvent.SourceCategory))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsDuplicateAsync(
        SellerFunnelEventType eventType,
        Guid sellerId,
        Guid? productId,
        Guid? cartId,
        Guid? orderId,
        Guid? buyerId,
        string? hashedVisitorId,
        string? idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await dbContext.SellerFunnelEvents.AnyAsync(
                funnelEvent => funnelEvent.SellerId == sellerId
                    && funnelEvent.EventType == eventType
                    && funnelEvent.IdempotencyKey == idempotencyKey,
                cancellationToken);
        }

        var dedupeAfter = now.Subtract(eventType is SellerFunnelEventType.StorefrontViewed or SellerFunnelEventType.ProductViewed
            ? ViewDedupeWindow
            : ActionDedupeWindow);

        return await dbContext.SellerFunnelEvents.AnyAsync(
            funnelEvent => funnelEvent.SellerId == sellerId
                && funnelEvent.EventType == eventType
                && funnelEvent.OccurredAtUtc >= dedupeAfter
                && funnelEvent.ProductId == productId
                && funnelEvent.CartId == cartId
                && funnelEvent.OrderId == orderId
                && ((buyerId.HasValue && funnelEvent.BuyerId == buyerId)
                    || (hashedVisitorId != null && funnelEvent.HashedAnonymousVisitorId == hashedVisitorId)),
            cancellationToken);
    }

    private static List<ValidationFailure> ValidateClientRequest(StorefrontFunnelEventRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.EventType is SellerFunnelEventType.OrderCreated or SellerFunnelEventType.OrderPaid)
        {
            failures.Add(new ValidationFailure("eventType", "Order funnel events are recorded by the server."));
        }

        if (!request.BuyerId.HasValue && string.IsNullOrWhiteSpace(request.AnonymousVisitorId))
        {
            failures.Add(new ValidationFailure("anonymousVisitorId", "Anonymous visitor id is required for public funnel events."));
        }

        if (!string.IsNullOrWhiteSpace(request.AnonymousVisitorId) && !IsValidVisitorId(request.AnonymousVisitorId))
        {
            failures.Add(new ValidationFailure("anonymousVisitorId", "Anonymous visitor id must be 8 to 128 characters and contain only letters, numbers, underscores, or hyphens."));
        }

        AddStringLengthFailure(failures, request.UtmSource, "utmSource", SellerFunnelEvent.UtmSourceMaxLength);
        AddStringLengthFailure(failures, request.UtmMedium, "utmMedium", SellerFunnelEvent.UtmMediumMaxLength);
        AddStringLengthFailure(failures, request.UtmCampaign, "utmCampaign", SellerFunnelEvent.UtmCampaignMaxLength);
        AddStringLengthFailure(failures, request.ReferrerHost, "referrerHost", SellerFunnelEvent.ReferrerHostMaxLength);
        AddStringLengthFailure(failures, request.SourceCategory, "sourceCategory", SellerFunnelEvent.SourceCategoryMaxLength);

        if (!string.IsNullOrWhiteSpace(request.ReferrerHost) && request.ReferrerHost.Contains('/', StringComparison.Ordinal))
        {
            failures.Add(new ValidationFailure("referrerHost", "Referrer host must contain only the host, not a full URL."));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceCategory)
            && !AllowedSourceCategories.Contains(request.SourceCategory.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(new ValidationFailure("sourceCategory", "sourceCategory must be Direct, Search, Social, Email, Ads, Referral, or Unknown."));
        }

        if (request.EventType is SellerFunnelEventType.ProductViewed or SellerFunnelEventType.ProductAddedToCart
            && (!request.ProductId.HasValue || request.ProductId.Value == Guid.Empty))
        {
            failures.Add(new ValidationFailure("productId", "Product id is required for product funnel events."));
        }

        if (request.EventType == SellerFunnelEventType.StorefrontViewed
            && string.IsNullOrWhiteSpace(request.SellerStoreSlug))
        {
            failures.Add(new ValidationFailure("sellerStoreSlug", "Seller storefront slug is required for storefront view events."));
        }

        if (request.EventType == SellerFunnelEventType.CheckoutStarted
            && (!request.CartId.HasValue || request.CartId.Value == Guid.Empty))
        {
            failures.Add(new ValidationFailure("cartId", "Cart id is required for checkout-start events."));
        }

        return failures;
    }

    private static void AddStringLengthFailure(
        List<ValidationFailure> failures,
        string? value,
        string fieldName,
        int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
        {
            failures.Add(new ValidationFailure(fieldName, $"{fieldName} must be {maxLength} characters or fewer."));
        }
    }

    private static bool IsValidVisitorId(string visitorId)
    {
        var trimmed = visitorId.Trim();
        return trimmed.Length is >= 8 and <= 128
            && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    }

    private static string? HashVisitorId(string? visitorId)
    {
        var trimmed = visitorId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed))).ToLowerInvariant();
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

    private static EventAttribution NormalizeAttribution(StorefrontFunnelEventRequest request)
    {
        var sourceCategory = NormalizeSourceCategory(request.SourceCategory);
        if (sourceCategory is null)
        {
            sourceCategory = HasAttributionSignal(request)
                ? "Referral"
                : "Direct";
        }

        return new EventAttribution(
            TrimOrNull(request.UtmSource, SellerFunnelEvent.UtmSourceMaxLength),
            TrimOrNull(request.UtmMedium, SellerFunnelEvent.UtmMediumMaxLength),
            TrimOrNull(request.UtmCampaign, SellerFunnelEvent.UtmCampaignMaxLength),
            TrimOrNull(request.ReferrerHost, SellerFunnelEvent.ReferrerHostMaxLength)?.ToLowerInvariant(),
            sourceCategory);
    }

    private static string? NormalizeSourceCategory(string? sourceCategory)
    {
        var trimmed = sourceCategory?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return AllowedSourceCategories.FirstOrDefault(category =>
            string.Equals(category, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAttributionSignal(StorefrontFunnelEventRequest request) =>
        !string.IsNullOrWhiteSpace(request.UtmSource)
        || !string.IsNullOrWhiteSpace(request.UtmMedium)
        || !string.IsNullOrWhiteSpace(request.UtmCampaign)
        || !string.IsNullOrWhiteSpace(request.ReferrerHost);

    private sealed record ClientEventTarget(Guid SellerId, Guid? ProductId, Guid? CartId, Guid? OrderId);

    private sealed record EventAttribution(
        string? UtmSource,
        string? UtmMedium,
        string? UtmCampaign,
        string? ReferrerHost,
        string? SourceCategory);
}
