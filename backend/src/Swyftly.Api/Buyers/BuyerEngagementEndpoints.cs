using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Catalog;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Notifications;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Api.Carts;
using Swyftly.Infrastructure.Notifications;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Buyers;

public static class BuyerEngagementEndpoints
{
    public static IEndpointRouteBuilder MapBuyerEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var wishlist = app.MapGroup("/api/buyer/wishlist")
            .WithTags("Buyer Wishlist")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        wishlist.MapGet("", GetWishlistAsync)
            .WithName("GetBuyerWishlist")
            .WithSummary("Returns the authenticated buyer's wishlist.")
            .Produces<IReadOnlyCollection<BuyerWishlistItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        wishlist.MapGet("/product-ids", GetWishlistProductIdsAsync)
            .WithName("GetBuyerWishlistProductIds")
            .WithSummary("Returns product ids saved by the authenticated buyer.")
            .Produces<BuyerWishlistProductIdsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        wishlist.MapPost("/{productId:guid}", AddWishlistItemAsync)
            .WithName("AddBuyerWishlistItem")
            .WithSummary("Adds a public product to the authenticated buyer's wishlist.")
            .Produces<BuyerWishlistItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        wishlist.MapPost("/{productId:guid}/move-to-cart", MoveWishlistItemToCartAsync)
            .WithName("MoveBuyerWishlistItemToCart")
            .WithSummary("Moves a wishlist item to the authenticated buyer's cart.")
            .Produces<CartResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        wishlist.MapDelete("/{productId:guid}", RemoveWishlistItemAsync)
            .WithName("RemoveBuyerWishlistItem")
            .WithSummary("Removes a product from the authenticated buyer's wishlist.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var buyerReviews = app.MapGroup("/api/buyer")
            .WithTags("Buyer Reviews")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerReviews.MapGet("/reviews", GetBuyerReviewsAsync)
            .WithName("GetBuyerReviews")
            .WithSummary("Returns product reviews created by the authenticated buyer.")
            .Produces<IReadOnlyCollection<BuyerProductReviewResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerReviews.MapPost("/orders/{orderId:guid}/items/{orderItemId:guid}/review", CreateReviewAsync)
            .WithName("CreateBuyerProductReview")
            .WithSummary("Creates a verified-purchase review for a delivered order item.")
            .Produces<BuyerProductReviewResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerReviews.MapPut("/reviews/{reviewId:guid}", UpdateReviewAsync)
            .WithName("UpdateBuyerProductReview")
            .WithSummary("Updates a product review owned by the authenticated buyer.")
            .Produces<BuyerProductReviewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerReviews.MapDelete("/reviews/{reviewId:guid}", DeleteReviewAsync)
            .WithName("DeleteBuyerProductReview")
            .WithSummary("Removes a product review owned by the authenticated buyer.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var notifications = app.MapGroup("/api/buyer/notifications")
            .WithTags("Buyer Notifications")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        notifications.MapGet("", GetNotificationsAsync)
            .WithName("GetBuyerNotifications")
            .WithSummary("Returns in-app notifications for the authenticated buyer.")
            .Produces<IReadOnlyCollection<NotificationResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        notifications.MapGet("/unread-count", GetUnreadNotificationCountAsync)
            .WithName("GetBuyerUnreadNotificationCount")
            .WithSummary("Returns the authenticated buyer's unread in-app notification count.")
            .Produces<NotificationsUnreadCountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        notifications.MapPost("/{notificationId:guid}/read", MarkNotificationReadAsync)
            .WithName("MarkBuyerNotificationRead")
            .WithSummary("Marks one in-app notification as read.")
            .Produces<NotificationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        notifications.MapPost("/read-all", MarkAllNotificationsReadAsync)
            .WithName("MarkAllBuyerNotificationsRead")
            .WithSummary("Marks all in-app notifications for the authenticated buyer as read.")
            .Produces<NotificationsReadAllResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/products/{slug}/reviews", GetPublicProductReviewsAsync)
            .WithTags("Product Reviews")
            .WithName("GetPublicProductReviews")
            .WithSummary("Returns published reviews for a public product.")
            .Produces<IReadOnlyCollection<PublicProductReviewResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/products/{slug}/review-summary", GetPublicProductReviewSummaryAsync)
            .WithTags("Product Reviews")
            .WithName("GetPublicProductReviewSummary")
            .WithSummary("Returns published review summary for a public product.")
            .Produces<PublicProductReviewSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetWishlistAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var wishlistItems = await dbContext.BuyerWishlistItems
            .Where(item => item.BuyerId == buyer.Id)
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var response = new List<BuyerWishlistItemResponse>();

        foreach (var item in wishlistItems)
        {
            var product = await CreateProductCardAsync(item.ProductId, dbContext, cancellationToken);
            if (product is not null)
            {
                response.Add(new BuyerWishlistItemResponse(
                    item.Id,
                    item.CreatedAtUtc,
                    product,
                    await GetWishlistVariantOptionsAsync(item.ProductId, dbContext, cancellationToken)));
            }
        }

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> GetWishlistProductIdsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var productIds = await dbContext.BuyerWishlistItems
            .Where(item => item.BuyerId == buyer.Id)
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.ProductId)
            .ToArrayAsync(cancellationToken);

        return HttpResults.Ok(new BuyerWishlistProductIdsResponse(productIds));
    }

    private static async Task<IResult> AddWishlistItemAsync(
        Guid productId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var product = await CreateProductCardAsync(productId, dbContext, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var existing = await dbContext.BuyerWishlistItems
            .SingleOrDefaultAsync(
                item => item.BuyerId == buyer.Id && item.ProductId == productId,
                cancellationToken);

        if (existing is not null)
        {
            return HttpResults.Ok(new BuyerWishlistItemResponse(
                existing.Id,
                existing.CreatedAtUtc,
                product,
                await GetWishlistVariantOptionsAsync(productId, dbContext, cancellationToken)));
        }

        var wishlistItem = new BuyerWishlistItem(buyer.Id, productId, timeProvider.GetUtcNow());
        dbContext.BuyerWishlistItems.Add(wishlistItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(new BuyerWishlistItemResponse(
            wishlistItem.Id,
            wishlistItem.CreatedAtUtc,
            product,
            await GetWishlistVariantOptionsAsync(productId, dbContext, cancellationToken)));
    }

    private static async Task<IResult> MoveWishlistItemToCartAsync(
        Guid productId,
        MoveWishlistItemToCartRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return Validation("quantity", "Quantity must be at least 1.");
        }

        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var wishlistItem = await dbContext.BuyerWishlistItems
            .SingleOrDefaultAsync(
                item => item.BuyerId == buyer.Id && item.ProductId == productId,
                cancellationToken);
        if (wishlistItem is null)
        {
            return WishlistItemNotFound();
        }

        var product = await BuildVisiblePublishedProductQuery(dbContext)
            .SingleOrDefaultAsync(existing => existing.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
            existing => existing.Id == request.ProductVariantId && existing.ProductId == product.Id,
            cancellationToken);
        if (variant is null)
        {
            return VariantNotFound();
        }

        if (variant.Status != ProductVariantStatus.Active)
        {
            return Validation("productVariantId", "Product variant is not available.");
        }

        var cart = await GetActiveCartAsync(buyer.Id, dbContext, cancellationToken);
        if (cart is null)
        {
            cart = new Cart(buyer.Id);
            dbContext.Carts.Add(cart);
        }

        try
        {
            cart.AddOrUpdateItem(
                product.Id,
                variant.Id,
                product.SellerId,
                product.Title,
                variant.Sku,
                variant.Size,
                variant.Colour,
                variant.Price,
                request.Quantity,
                variant.AvailableQuantity);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("cart", exception.Message);
        }

        dbContext.BuyerWishlistItems.Remove(wishlistItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CartEndpoints.CreateCartResponseAsync(cart, dbContext, cancellationToken));
    }

    private static async Task<IResult> RemoveWishlistItemAsync(
        Guid productId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var wishlistItem = await dbContext.BuyerWishlistItems
            .SingleOrDefaultAsync(
                item => item.BuyerId == buyer.Id && item.ProductId == productId,
                cancellationToken);
        if (wishlistItem is not null)
        {
            dbContext.BuyerWishlistItems.Remove(wishlistItem);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.NoContent();
    }

    private static async Task<IResult> GetBuyerReviewsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var reviews = await dbContext.ProductReviews
            .Where(review => review.BuyerId == buyer.Id && review.Status != ProductReviewStatus.Removed)
            .AsNoTracking()
            .OrderByDescending(review => review.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var productSummaries = await GetReviewProductSummariesAsync(
            reviews.Select(review => review.ProductId),
            dbContext,
            cancellationToken);

        return HttpResults.Ok(reviews
            .Select(review => MapBuyerReview(review, productSummaries))
            .ToArray());
    }

    private static async Task<IResult> CreateReviewAsync(
        Guid orderId,
        Guid orderItemId,
        ProductReviewRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReviewRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var order = await dbContext.Orders
            .Include(existing => existing.Items)
            .SingleOrDefaultAsync(
                existing => existing.Id == orderId && existing.BuyerId == buyer.Id,
                cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return Conflict("Reviews.OrderNotDelivered", "Reviews can only be created for delivered orders.");
        }

        var orderItem = order.Items.SingleOrDefault(item => item.Id == orderItemId);
        if (orderItem is null)
        {
            return OrderItemNotFound();
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == orderItem.ProductId, cancellationToken);
        if (product is null)
        {
            return ProductNotFound();
        }

        var duplicateExists = await dbContext.ProductReviews
            .AnyAsync(review => review.OrderItemId == orderItem.Id, cancellationToken);
        if (duplicateExists)
        {
            return Conflict("Reviews.DuplicateOrderItemReview", "This order item already has a review.");
        }

        var now = timeProvider.GetUtcNow();
        var review = new ProductReview(
            buyer.Id,
            order.SellerId,
            product.Id,
            order.Id,
            orderItem.Id,
            request.Rating,
            request.Title,
            request.Body,
            now);

        dbContext.ProductReviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summaries = await GetReviewProductSummariesAsync([review.ProductId], dbContext, cancellationToken);
        return HttpResults.Created(
            $"/api/buyer/reviews/{review.Id}",
            MapBuyerReview(review, summaries));
    }

    private static async Task<IResult> UpdateReviewAsync(
        Guid reviewId,
        ProductReviewRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validation = ValidateReviewRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var review = await dbContext.ProductReviews
            .SingleOrDefaultAsync(
                existing => existing.Id == reviewId && existing.BuyerId == buyer.Id,
                cancellationToken);
        if (review is null)
        {
            return ReviewNotFound();
        }

        if (review.Status == ProductReviewStatus.Removed)
        {
            return Conflict("Reviews.Removed", "Removed reviews cannot be updated.");
        }

        review.Update(request.Rating, request.Title, request.Body, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        var summaries = await GetReviewProductSummariesAsync([review.ProductId], dbContext, cancellationToken);
        return HttpResults.Ok(MapBuyerReview(review, summaries));
    }

    private static async Task<IResult> DeleteReviewAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var review = await dbContext.ProductReviews
            .SingleOrDefaultAsync(
                existing => existing.Id == reviewId && existing.BuyerId == buyer.Id,
                cancellationToken);
        if (review is null)
        {
            return ReviewNotFound();
        }

        review.Remove(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.NoContent();
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var notifications = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == buyer.UserId && notification.IsInAppVisible)
            .AsNoTracking()
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(notifications.Select(EfNotificationService.Map).ToArray());
    }

    private static async Task<IResult> MarkNotificationReadAsync(
        Guid notificationId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                existing => existing.Id == notificationId
                    && existing.RecipientUserId == buyer.UserId
                    && existing.IsInAppVisible,
                cancellationToken);
        if (notification is null)
        {
            return NotificationNotFound();
        }

        var readAtUtc = timeProvider.GetUtcNow();
        notification.MarkRead(readAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimePublisher.PublishNotificationReadAsync(
            buyer.UserId,
            notification.Id,
            notification.ReadAtUtc ?? readAtUtc,
            cancellationToken);

        return HttpResults.Ok(EfNotificationService.Map(notification));
    }

    private static async Task<IResult> GetUnreadNotificationCountAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var count = await dbContext.Notifications.CountAsync(
            notification => notification.RecipientUserId == buyer.UserId
                && notification.IsInAppVisible
                && notification.ReadAtUtc == null,
            cancellationToken);

        return HttpResults.Ok(new NotificationsUnreadCountResponse(count));
    }

    private static async Task<IResult> MarkAllNotificationsReadAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var unreadNotifications = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == buyer.UserId
                && notification.IsInAppVisible
                && notification.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var notification in unreadNotifications)
        {
            notification.MarkRead(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimePublisher.PublishNotificationsReadAllAsync(
            buyer.UserId,
            now,
            unreadNotifications.Count,
            cancellationToken);
        return HttpResults.Ok(new NotificationsReadAllResponse(unreadNotifications.Count));
    }

    private static async Task<IResult> GetPublicProductReviewsAsync(
        string slug,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var productId = await GetVisibleProductIdBySlugAsync(slug, dbContext, cancellationToken);
        if (!productId.HasValue)
        {
            return ProductNotFound();
        }

        var reviews = await dbContext.ProductReviews
            .Where(review => review.ProductId == productId && review.Status == ProductReviewStatus.Published)
            .AsNoTracking()
            .OrderByDescending(review => review.CreatedAtUtc)
            .Select(review => new PublicProductReviewResponse(
                review.Id,
                review.ProductId,
                review.Rating,
                review.Title,
                review.Body,
                review.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(reviews);
    }

    private static async Task<IResult> GetPublicProductReviewSummaryAsync(
        string slug,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var productId = await GetVisibleProductIdBySlugAsync(slug, dbContext, cancellationToken);
        if (!productId.HasValue)
        {
            return ProductNotFound();
        }

        var reviews = await dbContext.ProductReviews
            .Where(review => review.ProductId == productId && review.Status == ProductReviewStatus.Published)
            .AsNoTracking()
            .Select(review => review.Rating)
            .ToListAsync(cancellationToken);
        var count = reviews.Count;
        var average = count == 0 ? 0 : Math.Round(reviews.Average(), 2);
        var counts = Enumerable.Range(1, 5)
            .Select(rating => new ProductReviewRatingCountResponse(
                rating,
                reviews.Count(existing => existing == rating)))
            .ToArray();

        return HttpResults.Ok(new PublicProductReviewSummaryResponse(
            productId.Value,
            count,
            average,
            counts));
    }

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static async Task<Guid?> GetVisibleProductIdBySlugAsync(
        string slug,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return await BuildVisiblePublishedProductQuery(dbContext)
            .Where(product => product.Slug == normalizedSlug)
            .OrderByDescending(product => product.PublishedAtUtc)
            .Select(product => (Guid?)product.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<Cart?> GetActiveCartAsync(
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.BuyerId == buyerId && cart.Status == CartStatus.Active,
                cancellationToken);

    private static async Task<ProductSearchItemResponse?> CreateProductCardAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await BuildVisiblePublishedProductQuery(dbContext)
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var storefront = await dbContext.SellerStorefronts
            .AsNoTracking()
            .SingleOrDefaultAsync(storefront => storefront.SellerId == product.SellerId, cancellationToken);
        var primaryImage = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => image.ProductId == product.Id)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == product.Id && variant.Status == ProductVariantStatus.Active)
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return null;
        }

        return new ProductSearchItemResponse(
            product.Id,
            product.SellerId,
            storefront?.StoreName,
            storefront?.Slug,
            product.CategoryId,
            await GetCategoryPathAsync(product.CategoryId, dbContext, cancellationToken),
            product.BrandId,
            product.Title,
            product.Slug,
            product.ShortDescription,
            product.MerchandisingLabel,
            primaryImage?.Url,
            primaryImage?.AltText,
            variants.Min(variant => variant.Price),
            variants.Select(variant => variant.CompareAtPrice).Min(),
            variants.Any(variant => variant.StockQuantity > variant.ReservedQuantity),
            ReadStringArray(product.TagsJson),
            product.PublishedAtUtc);
    }

    private static async Task<IReadOnlyCollection<BuyerWishlistVariantOptionResponse>> GetWishlistVariantOptionsAsync(
        Guid productId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == productId && variant.Status == ProductVariantStatus.Active)
            .OrderBy(variant => variant.Size)
            .ThenBy(variant => variant.Colour)
            .Select(variant => new BuyerWishlistVariantOptionResponse(
                variant.Id,
                variant.Size,
                variant.Colour,
                variant.Price,
                variant.CompareAtPrice,
                variant.StockQuantity > variant.ReservedQuantity,
                variant.StockQuantity - variant.ReservedQuantity))
            .ToArrayAsync(cancellationToken);

    private static IQueryable<Product> BuildVisiblePublishedProductQuery(SwyftlyDbContext dbContext) =>
        dbContext.Products
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Published
                && dbContext.SellerProfiles.Any(seller =>
                    seller.Id == product.SellerId
                    && seller.VerificationStatus == SellerVerificationStatus.Verified)
                && dbContext.SellerStorefronts.Any(storefront =>
                    storefront.SellerId == product.SellerId
                    && storefront.IsPublished));

    private static async Task<Dictionary<Guid, BuyerReviewProductSummaryResponse>> GetReviewProductSummariesAsync(
        IEnumerable<Guid> productIds,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var products = await dbContext.Products
            .Where(product => ids.Contains(product.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var images = await dbContext.ProductImages
            .Where(image => ids.Contains(image.ProductId))
            .AsNoTracking()
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ToListAsync(cancellationToken);

        return products.ToDictionary(
            product => product.Id,
            product =>
            {
                var image = images.FirstOrDefault(existing => existing.ProductId == product.Id);
                return new BuyerReviewProductSummaryResponse(
                    product.Id,
                    product.SellerId,
                    product.Title,
                    product.Slug,
                    image?.Url,
                    image?.AltText);
            });
    }

    private static BuyerProductReviewResponse MapBuyerReview(
        ProductReview review,
        IReadOnlyDictionary<Guid, BuyerReviewProductSummaryResponse> productSummaries) =>
        new(
            review.Id,
            review.ProductId,
            review.OrderId,
            review.OrderItemId,
            review.Rating,
            review.Title,
            review.Body,
            review.Status.ToString(),
            review.ModerationReason,
            review.ModeratedAtUtc,
            review.CreatedAtUtc,
            review.UpdatedAtUtc,
            productSummaries.GetValueOrDefault(review.ProductId));

    private static async Task<string?> GetCategoryPathAsync(
        Guid? categoryId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var categories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var byId = categories.ToDictionary(category => category.Id);
        var names = new Stack<string>();
        var currentId = categoryId;

        while (currentId.HasValue && byId.TryGetValue(currentId.Value, out var category))
        {
            names.Push(category.Name);
            currentId = category.ParentCategoryId;
        }

        return names.Count == 0 ? null : string.Join(" > ", names);
    }

    private static IReadOnlyCollection<string> ReadStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IResult? ValidateReviewRequest(ProductReviewRequest request) =>
        request.Rating is < 1 or > 5
            ? HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rating"] = ["Rating must be between 1 and 5."]
            })
            : null;

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "BuyerEngagement.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ProductNotFound() =>
        HttpResults.Problem(
            title: "Products.NotFound",
            detail: "Product was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult VariantNotFound() =>
        HttpResults.Problem(
            title: "Cart.ProductVariantNotFound",
            detail: "Product variant was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult WishlistItemNotFound() =>
        HttpResults.Problem(
            title: "Wishlist.ItemNotFound",
            detail: "Wishlist item was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult OrderNotFound() =>
        HttpResults.Problem(
            title: "Reviews.OrderNotFound",
            detail: "Order was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult OrderItemNotFound() =>
        HttpResults.Problem(
            title: "Reviews.OrderItemNotFound",
            detail: "Order item was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ReviewNotFound() =>
        HttpResults.Problem(
            title: "Reviews.NotFound",
            detail: "Review was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult NotificationNotFound() =>
        HttpResults.Problem(
            title: "Notifications.NotFound",
            detail: "Notification was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult Conflict(string title, string detail) =>
        HttpResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);
}

public sealed record BuyerWishlistItemResponse(
    Guid WishlistItemId,
    DateTimeOffset CreatedAtUtc,
    ProductSearchItemResponse Product,
    IReadOnlyCollection<BuyerWishlistVariantOptionResponse> AvailableVariants);

public sealed record BuyerWishlistProductIdsResponse(IReadOnlyCollection<Guid> ProductIds);

public sealed record BuyerWishlistVariantOptionResponse(
    Guid ProductVariantId,
    string Size,
    string Colour,
    decimal Price,
    decimal? CompareAtPrice,
    bool InStock,
    int AvailableQuantity);

public sealed record MoveWishlistItemToCartRequest(
    Guid ProductVariantId,
    int Quantity);

public sealed record ProductReviewRequest(
    int Rating,
    string? Title,
    string? Body);

public sealed record BuyerProductReviewResponse(
    Guid ReviewId,
    Guid ProductId,
    Guid OrderId,
    Guid OrderItemId,
    int Rating,
    string? Title,
    string? Body,
    string Status,
    string? ModerationReason,
    DateTimeOffset? ModeratedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    BuyerReviewProductSummaryResponse? Product);

public sealed record BuyerReviewProductSummaryResponse(
    Guid ProductId,
    Guid SellerId,
    string? Title,
    string? Slug,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText);

public sealed record PublicProductReviewResponse(
    Guid ReviewId,
    Guid ProductId,
    int Rating,
    string? Title,
    string? Body,
    DateTimeOffset CreatedAtUtc);

public sealed record PublicProductReviewSummaryResponse(
    Guid ProductId,
    int ReviewCount,
    double AverageRating,
    IReadOnlyCollection<ProductReviewRatingCountResponse> RatingCounts);

public sealed record ProductReviewRatingCountResponse(
    int Rating,
    int Count);

public sealed record NotificationsReadAllResponse(int UpdatedCount);

public sealed record NotificationsUnreadCountResponse(int UnreadCount);
