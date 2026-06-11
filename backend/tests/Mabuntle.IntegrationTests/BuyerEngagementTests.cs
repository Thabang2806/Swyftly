using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Admin;
using Mabuntle.Api.Buyers;
using Mabuntle.Api.Carts;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;
using DomainNotification = Mabuntle.Domain.Notifications.Notification;

namespace Mabuntle.IntegrationTests;

public sealed class BuyerEngagementTests
{
    [Fact]
    public async Task Buyer_CanAddListAndRemoveWishlistItem()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-buyer@example.test", MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Wishlist Seller", "wishlist-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Wishlist Dress", "wishlist-dress", ProductSeedStatus.Published);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var addResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        addResponse.EnsureSuccessStatusCode();
        var added = await addResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse>();
        Assert.NotNull(added);
        Assert.Equal(productId, added!.Product.ProductId);

        using var duplicateResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        duplicateResponse.EnsureSuccessStatusCode();
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse>();
        Assert.Equal(added.WishlistItemId, duplicate!.WishlistItemId);

        using var listResponse = await client.GetAsync("/api/buyer/wishlist");
        listResponse.EnsureSuccessStatusCode();
        var wishlist = await listResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        var item = Assert.Single(wishlist!);
        Assert.Equal(productId, item.Product.ProductId);
        Assert.NotEmpty(item.AvailableVariants);

        using var productIdsResponse = await client.GetAsync("/api/buyer/wishlist/product-ids");
        productIdsResponse.EnsureSuccessStatusCode();
        var productIds = await productIdsResponse.Content.ReadFromJsonAsync<BuyerWishlistProductIdsResponse>();
        Assert.Contains(productId, productIds!.ProductIds);

        using var removeResponse = await client.DeleteAsync($"/api/buyer/wishlist/{productId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        using var emptyResponse = await client.GetAsync("/api/buyer/wishlist");
        emptyResponse.EnsureSuccessStatusCode();
        var emptyWishlist = await emptyResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        Assert.Empty(emptyWishlist!);
    }

    [Fact]
    public async Task Buyer_CanMoveWishlistItemToCart()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-cart-buyer@example.test", MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Wishlist Cart Seller", "wishlist-cart-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Wishlist Cart Dress", "wishlist-cart-dress", ProductSeedStatus.Published);
        var variantId = await GetVariantIdAsync(factory, productId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var addWishlistResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        addWishlistResponse.EnsureSuccessStatusCode();

        using var moveResponse = await client.PostAsJsonAsync(
            $"/api/buyer/wishlist/{productId}/move-to-cart",
            new MoveWishlistItemToCartRequest(variantId, 2));

        moveResponse.EnsureSuccessStatusCode();
        var cart = await moveResponse.Content.ReadFromJsonAsync<CartResponse>();
        var cartItem = Assert.Single(cart!.Items);
        Assert.Equal(productId, cartItem.ProductId);
        Assert.Equal(variantId, cartItem.ProductVariantId);
        Assert.Equal(2, cartItem.Quantity);

        using var listResponse = await client.GetAsync("/api/buyer/wishlist");
        listResponse.EnsureSuccessStatusCode();
        var wishlist = await listResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        Assert.Empty(wishlist!);
    }

    [Fact]
    public async Task MoveWishlistItemToCart_KeepsWishlistItemWhenCartRejectsMove()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-cart-conflict-buyer@example.test", MabuntleRoles.Buyer);
        var firstSellerId = await CreateSellerAsync(factory, "First Cart Seller", "first-cart-seller");
        var secondSellerId = await CreateSellerAsync(factory, "Second Cart Seller", "second-cart-seller");
        var cartProductId = await CreateProductAsync(factory, firstSellerId, "Cart Dress", "cart-dress", ProductSeedStatus.Published);
        var wishlistProductId = await CreateProductAsync(factory, secondSellerId, "Saved Dress", "saved-dress", ProductSeedStatus.Published);
        var cartVariantId = await GetVariantIdAsync(factory, cartProductId);
        var wishlistVariantId = await GetVariantIdAsync(factory, wishlistProductId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var cartResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(cartVariantId, 1));
        cartResponse.EnsureSuccessStatusCode();
        using var addWishlistResponse = await client.PostAsync($"/api/buyer/wishlist/{wishlistProductId}", null);
        addWishlistResponse.EnsureSuccessStatusCode();

        using var moveResponse = await client.PostAsJsonAsync(
            $"/api/buyer/wishlist/{wishlistProductId}/move-to-cart",
            new MoveWishlistItemToCartRequest(wishlistVariantId, 1));

        Assert.Equal(HttpStatusCode.BadRequest, moveResponse.StatusCode);
        using var listResponse = await client.GetAsync("/api/buyer/wishlist");
        listResponse.EnsureSuccessStatusCode();
        var wishlist = await listResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        Assert.Contains(wishlist!, item => item.Product.ProductId == wishlistProductId);
    }

    [Fact]
    public async Task MoveWishlistItemToCart_RejectsInvalidVariantAndQuantity()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-cart-invalid-buyer@example.test", MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Invalid Move Seller", "invalid-move-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Invalid Move Dress", "invalid-move-dress", ProductSeedStatus.Published);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var addWishlistResponse = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);
        addWishlistResponse.EnsureSuccessStatusCode();

        using var invalidQuantityResponse = await client.PostAsJsonAsync(
            $"/api/buyer/wishlist/{productId}/move-to-cart",
            new MoveWishlistItemToCartRequest(Guid.NewGuid(), 0));
        using var invalidVariantResponse = await client.PostAsJsonAsync(
            $"/api/buyer/wishlist/{productId}/move-to-cart",
            new MoveWishlistItemToCartRequest(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.BadRequest, invalidQuantityResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidVariantResponse.StatusCode);
        using var listResponse = await client.GetAsync("/api/buyer/wishlist");
        listResponse.EnsureSuccessStatusCode();
        var wishlist = await listResponse.Content.ReadFromJsonAsync<BuyerWishlistItemResponse[]>();
        Assert.Single(wishlist!);
    }

    [Fact]
    public async Task Wishlist_RejectsProductsThatAreNotPubliclyVisible()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "wishlist-hidden-buyer@example.test", MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(
            factory,
            "Hidden Wishlist Seller",
            "hidden-wishlist-seller",
            publishStorefront: false);
        var productId = await CreateProductAsync(factory, sellerId, "Hidden Dress", "hidden-dress", ProductSeedStatus.Published);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PostAsync($"/api/buyer/wishlist/{productId}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_CanCreateUpdateAndDeleteVerifiedPurchaseReview()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Review Seller", "review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Review Dress", "review-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(5, "Great fit", "The dress matched the description."));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.NotNull(created);
        Assert.Equal(productId, created!.ProductId);
        Assert.Equal("PendingReview", created.Status);

        using var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(4, "Duplicate", "Second review."));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/buyer/reviews/{created.ReviewId}",
            new ProductReviewRequest(4, "Updated fit", "Still happy after a second look."));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.Equal(4, updated!.Rating);
        Assert.Equal("Updated fit", updated.Title);
        Assert.Equal("PendingReview", updated.Status);

        using var deleteResponse = await client.DeleteAsync($"/api/buyer/reviews/{created.ReviewId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var buyerReviewsResponse = await client.GetAsync("/api/buyer/reviews");
        buyerReviewsResponse.EnsureSuccessStatusCode();
        var buyerReviews = await buyerReviewsResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse[]>();
        Assert.Empty(buyerReviews!);
    }

    [Fact]
    public async Task Buyer_CannotReviewUndeliveredOrOtherBuyerOrders()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string ownerEmail = "review-owner@example.test";
        const string otherEmail = "review-other@example.test";
        await RegisterAndLoginAsync(client, ownerEmail, MabuntleRoles.Buyer);
        var otherToken = await RegisterAndLoginAsync(client, otherEmail, MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Review Guard Seller", "review-guard-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Guard Dress", "guard-dress", ProductSeedStatus.Published);
        var owner = await GetBuyerAsync(factory, ownerEmail);
        var pendingOrder = await CreatePendingOrderAsync(factory, owner.Id, sellerId, productId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        using var otherBuyerResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{pendingOrder.OrderId}/items/{pendingOrder.OrderItemId}/review",
            new ProductReviewRequest(5, "Not mine", "This should not be allowed."));
        Assert.Equal(HttpStatusCode.NotFound, otherBuyerResponse.StatusCode);

        var ownerToken = await LoginAsync(client, ownerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var undeliveredResponse = await client.PostAsJsonAsync(
            $"/api/buyer/orders/{pendingOrder.OrderId}/items/{pendingOrder.OrderItemId}/review",
            new ProductReviewRequest(5, "Too soon", "This should not be allowed yet."));
        Assert.Equal(HttpStatusCode.Conflict, undeliveredResponse.StatusCode);
    }

    [Fact]
    public async Task PublicReviewReads_ReturnOnlyPublishedReviewsAndSummary()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory, "Public Review Seller", "public-review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Public Review Dress", "public-review-dress", ProductSeedStatus.Published);
        await SeedReviewsAsync(factory, productId, sellerId);

        using var listResponse = await client.GetAsync("/api/products/public-review-dress/reviews");
        listResponse.EnsureSuccessStatusCode();
        var reviews = await listResponse.Content.ReadFromJsonAsync<PublicProductReviewResponse[]>();
        var review = Assert.Single(reviews!);
        Assert.Equal(5, review.Rating);

        using var summaryResponse = await client.GetAsync("/api/products/public-review-dress/review-summary");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<PublicProductReviewSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.ReviewCount);
        Assert.Equal(5, summary.AverageRating);
        Assert.Equal(1, summary.RatingCounts.Single(count => count.Rating == 5).Count);
    }

    [Fact]
    public async Task AdminCanModeratePendingBuyerReviews_AndBuyerIsNotified()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var buyerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        const string buyerEmail = "moderated-review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(buyerClient, buyerEmail, MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Moderation Seller", "moderation-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Moderated Dress", "moderated-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(5, "Helpful fit", "The product matched the photos."));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();
        Assert.Equal("PendingReview", created!.Status);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "review-admin@example.test", MabuntleRoles.Admin));

        using var pendingResponse = await adminClient.GetAsync("/api/admin/reviews/pending");
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<AdminProductReviewDetailResponse[]>();
        Assert.Contains(pending!, review => review.ReviewId == created.ReviewId);

        using var approveResponse = await adminClient.PostAsync($"/api/admin/reviews/{created.ReviewId}/approve", null);
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<AdminProductReviewDetailResponse>();
        Assert.Equal("Published", approved!.Status);
        Assert.Contains(approved.AuditTrail, audit => audit.ActionType == "ProductReviewApproved");

        using var publicResponse = await buyerClient.GetAsync("/api/products/moderated-dress/reviews");
        publicResponse.EnsureSuccessStatusCode();
        var publicReviews = await publicResponse.Content.ReadFromJsonAsync<PublicProductReviewResponse[]>();
        Assert.Contains(publicReviews!, review => review.ReviewId == created.ReviewId);

        using var notificationResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationResponse.EnsureSuccessStatusCode();
        var notifications = await notificationResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReviewApproved" && notification.RelatedEntityId == created.ReviewId);
    }

    [Fact]
    public async Task AdminCanRejectPendingBuyerReview_AndBuyerSeesReason()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var buyerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        const string buyerEmail = "rejected-review-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(buyerClient, buyerEmail, MabuntleRoles.Buyer);
        var sellerId = await CreateSellerAsync(factory, "Rejected Review Seller", "rejected-review-seller");
        var productId = await CreateProductAsync(factory, sellerId, "Rejected Review Dress", "rejected-review-dress", ProductSeedStatus.Published);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var orderSeed = await CreateDeliveredOrderAsync(factory, buyer.Id, sellerId, productId);
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderSeed.OrderId}/items/{orderSeed.OrderItemId}/review",
            new ProductReviewRequest(2, "Bad words", "Needs moderation."));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse>();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginUserInRoleAsync(factory, adminClient, "reject-review-admin@example.test", MabuntleRoles.Admin));
        using var rejectResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/reviews/{created!.ReviewId}/reject",
            new AdminProductReviewReasonRequest("Please remove personal information."));
        rejectResponse.EnsureSuccessStatusCode();

        using var buyerReviewsResponse = await buyerClient.GetAsync("/api/buyer/reviews");
        buyerReviewsResponse.EnsureSuccessStatusCode();
        var buyerReviews = await buyerReviewsResponse.Content.ReadFromJsonAsync<BuyerProductReviewResponse[]>();
        var rejected = Assert.Single(buyerReviews!);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Please remove personal information.", rejected.ModerationReason);

        using var notificationResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationResponse.EnsureSuccessStatusCode();
        var notifications = await notificationResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReviewRejected" && notification.RelatedEntityId == created.ReviewId);
    }

    [Fact]
    public async Task Buyer_CanListAndMarkNotificationsRead()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "notification-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, MabuntleRoles.Buyer);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        var notificationIds = await SeedNotificationsAsync(factory, buyer.UserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var listResponse = await client.GetAsync("/api/buyer/notifications");
        listResponse.EnsureSuccessStatusCode();
        var notifications = await listResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Equal(2, notifications!.Length);
        Assert.All(notifications, notification => Assert.Null(notification.ReadAtUtc));

        using var countResponse = await client.GetAsync("/api/buyer/notifications/unread-count");
        countResponse.EnsureSuccessStatusCode();
        var count = await countResponse.Content.ReadFromJsonAsync<NotificationsUnreadCountResponse>();
        Assert.Equal(2, count!.UnreadCount);

        using var readResponse = await client.PostAsync($"/api/buyer/notifications/{notificationIds[0]}/read", null);
        readResponse.EnsureSuccessStatusCode();
        var read = await readResponse.Content.ReadFromJsonAsync<NotificationResult>();
        Assert.NotNull(read!.ReadAtUtc);
        var readEvent = Assert.Single(factory.RealtimePublisher.Read);
        Assert.Equal(notificationIds[0], readEvent.NotificationId);

        using var readAllResponse = await client.PostAsync("/api/buyer/notifications/read-all", null);
        readAllResponse.EnsureSuccessStatusCode();
        var readAll = await readAllResponse.Content.ReadFromJsonAsync<NotificationsReadAllResponse>();
        Assert.Equal(1, readAll!.UpdatedCount);
        var readAllEvent = Assert.Single(factory.RealtimePublisher.ReadAll);
        Assert.Equal(1, readAllEvent.UpdatedCount);
    }

    [Fact]
    public async Task NotificationHub_NegotiateRequiresBuyerOrSellerAuthentication()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();

        using var anonymousResponse = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var buyerToken = await RegisterAndLoginAsync(client, "hub-buyer@example.test", MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var buyerResponse = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        buyerResponse.EnsureSuccessStatusCode();

        var sellerToken = await RegisterAndLoginAsync(client, "hub-seller@example.test", MabuntleRoles.Seller);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        using var sellerResponse = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        sellerResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Buyer_CanReadAndUpdateProfileSettings()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "settings-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var getResponse = await client.GetAsync("/api/buyer/profile");
        getResponse.EnsureSuccessStatusCode();
        var initial = await getResponse.Content.ReadFromJsonAsync<BuyerProfileSettingsResponse>();
        Assert.Equal(buyerEmail, initial!.Email);
        Assert.Null(initial.DisplayName);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/buyer/profile",
            new BuyerProfileSettingsRequest("Thabo", "+27110000000"));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<BuyerProfileSettingsResponse>();
        Assert.Equal("Thabo", updated!.DisplayName);
        Assert.Equal("+27110000000", updated.PhoneNumber);
    }

    [Fact]
    public async Task BuyerProfileSettings_RejectsOverlongFields()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "settings-invalid-buyer@example.test", MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PutAsJsonAsync(
            "/api/buyer/profile",
            new BuyerProfileSettingsRequest(new string('A', BuyerProfile.DisplayNameMaxLength + 1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_CanReadAndUpdateNotificationPreferences()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "preference-buyer@example.test", MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var getResponse = await client.GetAsync("/api/buyer/notification-preferences");
        getResponse.EnsureSuccessStatusCode();
        var initial = await getResponse.Content.ReadFromJsonAsync<BuyerNotificationPreferencesResponse>();
        Assert.Equal(BuyerNotificationCategory.All.Count, initial!.Preferences.Count);
        Assert.All(initial.Preferences, preference => Assert.True(preference.IsEnabled));
        Assert.All(initial.Preferences, preference => Assert.True(preference.EmailEnabled));

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/buyer/notification-preferences",
            new BuyerNotificationPreferencesRequest([
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Orders, true),
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Returns, true),
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Reviews, false, false),
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Support, true)
            ]));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<BuyerNotificationPreferencesResponse>();
        var reviewPreference = updated!.Preferences.Single(preference => preference.Category == BuyerNotificationCategory.Reviews);
        Assert.False(reviewPreference.IsEnabled);
        Assert.False(reviewPreference.EmailEnabled);
        Assert.True(updated.Preferences.Single(preference => preference.Category == BuyerNotificationCategory.Orders).IsEnabled);
    }

    [Fact]
    public async Task Buyer_CanManageDeliveryAddressesWithDefaultBehavior()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "delivery-address-buyer@example.test", MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            DeliveryAddressRequest("Home", "Home Recipient", isDefault: false));
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse>();
        Assert.True(first!.IsDefault);
        Assert.Equal("Leave at reception.", first.DeliveryInstructions);

        using var secondResponse = await client.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            DeliveryAddressRequest("Work", "Work Recipient", isDefault: true));
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse>();
        Assert.True(second!.IsDefault);

        using var listResponse = await client.GetAsync("/api/buyer/delivery-addresses");
        listResponse.EnsureSuccessStatusCode();
        var addresses = await listResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse[]>();
        Assert.Equal(2, addresses!.Length);
        Assert.Single(addresses, address => address.IsDefault);
        Assert.Equal(second.DeliveryAddressId, addresses.Single(address => address.IsDefault).DeliveryAddressId);

        using var makeDefaultResponse = await client.PostAsync(
            $"/api/buyer/delivery-addresses/{first.DeliveryAddressId}/make-default",
            null);
        makeDefaultResponse.EnsureSuccessStatusCode();
        var afterDefault = await makeDefaultResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse[]>();
        Assert.Equal(first.DeliveryAddressId, afterDefault!.Single(address => address.IsDefault).DeliveryAddressId);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/buyer/delivery-addresses/{first.DeliveryAddressId}",
            DeliveryAddressRequest("Updated home", "Updated Recipient", isDefault: false));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse>();
        Assert.Equal("Updated home", updated!.Label);
        Assert.True(updated.IsDefault);

        using var deleteResponse = await client.DeleteAsync($"/api/buyer/delivery-addresses/{first.DeliveryAddressId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var finalListResponse = await client.GetAsync("/api/buyer/delivery-addresses");
        finalListResponse.EnsureSuccessStatusCode();
        var remaining = await finalListResponse.Content.ReadFromJsonAsync<BuyerDeliveryAddressResponse[]>();
        var remainingAddress = Assert.Single(remaining!);
        Assert.Equal(second.DeliveryAddressId, remainingAddress.DeliveryAddressId);
        Assert.True(remainingAddress.IsDefault);
    }

    [Fact]
    public async Task DeliveryAddressValidation_RejectsInvalidAndTooManyAddresses()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, "delivery-address-invalid-buyer@example.test", MabuntleRoles.Buyer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var invalidCountryResponse = await client.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            DeliveryAddressRequest("Invalid", "Invalid Recipient", countryCode: "South Africa"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCountryResponse.StatusCode);

        for (var index = 0; index < BuyerDeliveryAddress.MaxAddressesPerBuyer; index++)
        {
            using var createResponse = await client.PostAsJsonAsync(
                "/api/buyer/delivery-addresses",
                DeliveryAddressRequest($"Address {index}", $"Recipient {index}", isDefault: index == 0));
            createResponse.EnsureSuccessStatusCode();
        }

        using var tooManyResponse = await client.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            DeliveryAddressRequest("Too many", "Too many recipient"));
        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);
    }

    [Fact]
    public async Task NotificationPreferences_ControlInAppAndEmailDeliveryChannels()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();
        const string buyerEmail = "suppressed-notification-buyer@example.test";
        var buyerToken = await RegisterAndLoginAsync(client, buyerEmail, MabuntleRoles.Buyer);
        var buyer = await GetBuyerAsync(factory, buyerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/buyer/notification-preferences",
            new BuyerNotificationPreferencesRequest([
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Reviews, false, true),
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Returns, true, false),
                new BuyerNotificationPreferenceRequest(BuyerNotificationCategory.Support, false, false)
            ]));
        updateResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var emailOnly = await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    buyer.UserId,
                    "ReviewApproved",
                    "Your review was published",
                    "Your review is visible.",
                    "ProductReview",
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow));
            var inAppOnly = await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    buyer.UserId,
                    "ReturnApproved",
                    "Your return was approved",
                    "Your return was approved.",
                    "ReturnRequest",
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            var fullySuppressed = await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    buyer.UserId,
                    "SupportReply",
                    "Support replied",
                    "Support replied to your ticket.",
                    "SupportTicket",
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddMinutes(2)));
            var unknown = await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    buyer.UserId,
                    "CustomNotice",
                    "Custom notice",
                    "This unknown category should still be created.",
                    null,
                    null,
                    DateTimeOffset.UtcNow.AddMinutes(3)));

            Assert.NotNull(emailOnly);
            Assert.False(emailOnly!.ReadAtUtc.HasValue);
            Assert.NotNull(inAppOnly);
            Assert.Null(fullySuppressed);
            Assert.NotNull(unknown);

            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var emailDeliveries = await dbContext.NotificationEmailDeliveries
                .AsNoTracking()
                .ToListAsync();
            Assert.Single(emailDeliveries);
            Assert.Equal(emailOnly.NotificationId, emailDeliveries.Single().NotificationId);
        }

        Assert.DoesNotContain(factory.RealtimePublisher.Created, notification => notification.Type == "ReviewApproved");
        Assert.Contains(factory.RealtimePublisher.Created, notification => notification.Type == "ReturnApproved");
        Assert.Contains(factory.RealtimePublisher.Created, notification => notification.Type == "CustomNotice");

        using var notificationResponse = await client.GetAsync("/api/buyer/notifications");
        notificationResponse.EnsureSuccessStatusCode();
        var notifications = await notificationResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.DoesNotContain(notifications!, notification => notification.Type == "ReviewApproved");
        Assert.Contains(notifications!, notification => notification.Type == "ReturnApproved");
        Assert.DoesNotContain(notifications!, notification => notification.Type == "SupportReply");
        Assert.Contains(notifications!, notification => notification.Type == "CustomNotice");
    }

    [Fact]
    public async Task BuyerEndpoints_RejectAnonymousAndNonBuyerUsers()
    {
        using var factory = new BuyerEngagementTestFactory();
        using var client = factory.CreateClient();

        using var anonymousResponse = await client.GetAsync("/api/buyer/wishlist");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        using var anonymousSettingsResponse = await client.GetAsync("/api/buyer/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSettingsResponse.StatusCode);
        using var anonymousAddressResponse = await client.GetAsync("/api/buyer/delivery-addresses");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAddressResponse.StatusCode);

        var sellerToken = await RegisterAndLoginAsync(client, "engagement-seller@example.test", MabuntleRoles.Seller);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        using var sellerResponse = await client.GetAsync("/api/buyer/wishlist");
        Assert.Equal(HttpStatusCode.Forbidden, sellerResponse.StatusCode);
        using var sellerSettingsResponse = await client.GetAsync("/api/buyer/profile");
        Assert.Equal(HttpStatusCode.Forbidden, sellerSettingsResponse.StatusCode);
        using var sellerAddressResponse = await client.GetAsync("/api/buyer/delivery-addresses");
        Assert.Equal(HttpStatusCode.Forbidden, sellerAddressResponse.StatusCode);
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task<string> CreateAndLoginUserInRoleAsync(
        BuyerEngagementTestFactory factory,
        HttpClient client,
        string email,
        string role)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createResult = await userManager.CreateAsync(user, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<BuyerProfile> GetBuyerAsync(BuyerEngagementTestFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.Email == email);
        return await dbContext.BuyerProfiles.SingleAsync(buyer => buyer.UserId == user.Id);
    }

    private static async Task<Guid> CreateSellerAsync(
        BuyerEngagementTestFactory factory,
        string storeName,
        string storeSlug,
        bool publishStorefront = true)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            storeName,
            $"{storeSlug}@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            $"{storeName} Trading");
        var storefront = new SellerStorefront(seller.Id, storeName, storeSlug);
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        if (publishStorefront)
        {
            storefront.Publish();
        }

        dbContext.AddRange(seller, storefront, address, payout);
        await dbContext.SaveChangesAsync();
        return seller.Id;
    }

    private static async Task<Guid> CreateProductAsync(
        BuyerEngagementTestFactory factory,
        Guid sellerId,
        string title,
        string slug,
        ProductSeedStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            title,
            slug,
            "A marketplace-ready dress.",
            "A dress for buyer engagement testing.");
        product.UpdateTags("[\"dress\",\"review\"]");

        if (status == ProductSeedStatus.Published)
        {
            product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
            product.Publish(DateTimeOffset.UtcNow);
        }

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499m,
            599m,
            stockQuantity: 10));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            $"https://example.test/{slug}.jpg",
            $"products/{product.Id:N}/primary.jpg",
            title,
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        return product.Id;
    }

    private static async Task<Guid> GetVariantIdAsync(BuyerEngagementTestFactory factory, Guid productId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.ProductVariants
            .Where(variant => variant.ProductId == productId)
            .Select(variant => variant.Id)
            .SingleAsync();
    }

    private static async Task<OrderSeed> CreateDeliveredOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId) =>
        await CreateOrderAsync(factory, buyerId, sellerId, productId, delivered: true);

    private static async Task<OrderSeed> CreatePendingOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId) =>
        await CreateOrderAsync(factory, buyerId, sellerId, productId, delivered: false);

    private static async Task<OrderSeed> CreateOrderAsync(
        BuyerEngagementTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        Guid productId,
        bool delivered)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var now = DateTimeOffset.UtcNow;
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(productId, Guid.NewGuid(), "Review Dress", "SKU-ORDER-1", "M", "Black", 499m, 1);
        if (delivered)
        {
            order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "Paid");
            order.ChangeStatus(OrderStatus.Processing, now.AddMinutes(2), "Processing");
            order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(3), "Shipped");
            order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(4), "Delivered");
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return new OrderSeed(order.Id, order.Items.Single().Id);
    }

    private static async Task SeedReviewsAsync(BuyerEngagementTestFactory factory, Guid productId, Guid sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var publicReview = new ProductReview(
            buyer.Id,
            sellerId,
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            5,
            "Excellent",
            "Loved the fit.",
            now);
        publicReview.Approve(Guid.NewGuid(), now.AddMinutes(1));
        var removedReview = new ProductReview(
            buyer.Id,
            sellerId,
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Removed",
            "This should not be public.",
            now);
        removedReview.Remove(now.AddMinutes(1));

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.ProductReviews.AddRange(publicReview, removedReview);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<Guid>> SeedNotificationsAsync(BuyerEngagementTestFactory factory, Guid recipientUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var first = new DomainNotification(
            recipientUserId,
            "OrderUpdate",
            "Order shipped",
            "Your order has shipped.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var second = new DomainNotification(
            recipientUserId,
            "Support",
            "Support reply",
            "Support replied to your ticket.",
            "SupportTicket",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(1));

        dbContext.Notifications.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        return [first.Id, second.Id];
    }

    private static BuyerDeliveryAddressRequest DeliveryAddressRequest(
        string label,
        string recipientName,
        bool isDefault = false,
        string countryCode = "ZA") =>
        new(
            label,
            recipientName,
            "+27110000000",
            "10 Market Street",
            "Apartment 4",
            "Rosebank",
            "Johannesburg",
            "Gauteng",
            "2196",
            countryCode,
            isDefault,
            "Leave at reception.");

    private enum ProductSeedStatus
    {
        Draft,
        Published
    }

    private sealed record OrderSeed(Guid OrderId, Guid OrderItemId);

    private sealed class BuyerEngagementTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleBuyerEngagementTests_{Guid.NewGuid():N}";

        public RecordingNotificationRealtimePublisher RealtimePublisher { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();
                services.RemoveAll<INotificationRealtimePublisher>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddSingleton<INotificationRealtimePublisher>(RealtimePublisher);
                services.AddDbContext<MabuntleDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }

    public sealed class RecordingNotificationRealtimePublisher : INotificationRealtimePublisher
    {
        public List<NotificationResult> Created { get; } = [];

        public List<NotificationReadRealtimeEvent> Read { get; } = [];

        public List<NotificationsReadAllRealtimeEvent> ReadAll { get; } = [];

        public Task PublishNotificationCreatedAsync(
            NotificationResult notification,
            CancellationToken cancellationToken = default)
        {
            Created.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishNotificationReadAsync(
            Guid recipientUserId,
            Guid notificationId,
            DateTimeOffset readAtUtc,
            CancellationToken cancellationToken = default)
        {
            Read.Add(new NotificationReadRealtimeEvent(notificationId, readAtUtc));
            return Task.CompletedTask;
        }

        public Task PublishNotificationsReadAllAsync(
            Guid recipientUserId,
            DateTimeOffset readAtUtc,
            int updatedCount,
            CancellationToken cancellationToken = default)
        {
            ReadAll.Add(new NotificationsReadAllRealtimeEvent(readAtUtc, updatedCount));
            return Task.CompletedTask;
        }
    }
}
