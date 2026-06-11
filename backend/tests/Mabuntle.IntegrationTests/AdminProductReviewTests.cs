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
using Mabuntle.Api.Admin;
using Mabuntle.Api.Authentication;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminProductReviewTests
{
    [Fact]
    public async Task Buyer_CannotAccessAdminProductEndpoints()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/products/pending-review");
        using var moderationResponse = await client.GetAsync("/api/admin/products/moderation-items");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, moderationResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListPendingReviewProducts()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/products/pending-review");

        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<AdminProductSummaryResponse[]>();
        Assert.NotNull(products);
        var product = Assert.Single(products!, item => item.ProductId == productId);
        Assert.Equal("PendingReview", product.Status);
    }

    [Fact]
    public async Task Admin_CanListProductModerationItems_AcrossProductAndRevisionTypes()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var pendingProductId = await CreateReviewProductAsync(factory);
        var variantSeed = await CreatePublishedProductWithVariantRevisionAsync(factory);
        var listingRevisionId = await CreatePendingListingRevisionAsync(factory, variantSeed.ProductId);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var needsAttentionResponse = await client.GetAsync("/api/admin/products/moderation-items");
        needsAttentionResponse.EnsureSuccessStatusCode();
        var needsAttention = await needsAttentionResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminProductModerationItemResponse>>();
        Assert.NotNull(needsAttention);
        Assert.Contains(needsAttention!.Items, item =>
            item.ItemType == "Product"
            && item.ProductId == pendingProductId
            && item.DetailRoute == $"/admin/products/{pendingProductId}");
        Assert.Contains(needsAttention.Items, item =>
            item.ItemType == "ListingRevision"
            && item.RevisionId == listingRevisionId
            && item.DetailRoute == $"/admin/products/revisions/{listingRevisionId}");
        Assert.Contains(needsAttention.Items, item =>
            item.ItemType == "VariantRevision"
            && item.RevisionId == variantSeed.RevisionId
            && item.DetailRoute == $"/admin/products/variant-revisions/{variantSeed.RevisionId}");
        Assert.Contains(needsAttention.StatusCounts, count => count.Status == "PendingReview" && count.Count >= 3);

        using var publishedResponse = await client.GetAsync("/api/admin/products/moderation-items?view=All&status=Published&search=Published%20Variant");
        publishedResponse.EnsureSuccessStatusCode();
        var published = await publishedResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminProductModerationItemResponse>>();
        Assert.NotNull(published);
        var publishedProduct = Assert.Single(published!.Items, item => item.ProductId == variantSeed.ProductId && item.ItemType == "Product");
        Assert.Equal("Published", publishedProduct.Status);
    }

    [Fact]
    public async Task Approve_PublishesVerifiedSellerProduct_AndWritesAuditLog()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest());

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Published", detail!.Status);
        Assert.Contains(detail.AuditTrail, entry => entry.ActionType == "ProductApproved");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.ProductApproved
            && notification.RelatedEntityId == productId));
    }

    [Fact]
    public async Task Reject_RequiresReason_AndWritesAuditLog()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/reject",
            new AdminProductReasonRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/reject",
            new AdminProductReasonRequest("Listing images do not match the product."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Rejected", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductRejected" && entry.Reason == "Listing images do not match the product.");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.ProductRejected
            && notification.RelatedEntityId == productId));
    }

    [Fact]
    public async Task RequestChanges_RequiresReason_AndMovesProductToChangesRequested()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/request-changes",
            new AdminProductReasonRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/request-changes",
            new AdminProductReasonRequest("Add clearer size measurements."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("ChangesRequested", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductChangesRequested" && entry.Reason == "Add clearer size measurements.");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.ProductChangesRequested
            && notification.RelatedEntityId == productId));
    }

    [Fact]
    public async Task Approve_HighRiskModerationRequiresOverrideReason()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var productId = await CreateReviewProductAsync(factory, needsAdminReview: true);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var blockedResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest());

        Assert.Equal(HttpStatusCode.BadRequest, blockedResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{productId}/approve",
            new AdminProductApproveRequest("Reviewed supplier documents manually."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Published", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "ProductApproved" && entry.Reason == "Reviewed supplier documents manually.");
    }

    [Fact]
    public async Task ApproveVariantRevision_AppliesChangesAndUpdatesActiveCartSnapshots()
    {
        using var factory = new AdminProductReviewTestFactory();
        using var client = factory.CreateClient();
        var seed = await CreatePublishedProductWithVariantRevisionAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/variant-revisions/{seed.RevisionId}/approve",
            new { });

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminProductVariantRevisionDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Approved", detail!.Status);
        Assert.Contains(detail.AuditTrail, entry => entry.ActionType == "ProductVariantRevisionApproved");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.Id == seed.VariantId);
        var cartItem = await dbContext.CartItems.SingleAsync(item => item.Id == seed.CartItemId);

        Assert.Equal("UPDATED-SKU-M-BLACK", variant.Sku);
        Assert.Equal(599.99m, variant.Price);
        Assert.Equal("UPDATED-SKU-M-BLACK", cartItem.Sku);
        Assert.Equal(599.99m, cartItem.UnitPrice);
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.ProductVariantRevisionApproved
            && notification.RelatedEntityId == seed.ProductId));
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-product-admin@example.test";
        await RegisterAsync(client, email, MabuntleRoles.Buyer);
        return await LoginAsync(client, email);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminProductReviewTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-products-{Guid.NewGuid():N}@example.test";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, "Password123!");
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

            var roleResult = await userManager.AddToRoleAsync(admin, MabuntleRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return await LoginAsync(client, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task<Guid> CreateReviewProductAsync(
        AdminProductReviewTestFactory factory,
        bool needsAdminReview = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Review Seller",
            "review-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Review Seller Trading");

        var storefront = new SellerStorefront(seller.Id, "Review Seller", $"review-seller-{Guid.NewGuid():N}");
        var address = new SellerAddress(
            seller.Id,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            needsAdminReview ? "Designer inspired dress" : "Summer Dress",
            $"admin-review-product-{Guid.NewGuid():N}",
            "A lightweight summer dress.",
            needsAdminReview
                ? "A mirror quality look for evening events."
                : "A lightweight summer dress with a relaxed fit.");
        product.SubmitForReview(
            hasAtLeastOneImage: true,
            hasAtLeastOneActiveVariant: true,
            needsAdminReview);

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        dbContext.Products.Add(product);
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "size", "\"M\""));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "colour", "\"Black\""));
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499.99m,
            699.99m,
            10));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            "https://example.test/summer-dress.jpg",
            $"products/{product.Id:N}/primary.jpg",
            "Summer dress",
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));

        if (needsAdminReview)
        {
            dbContext.AiModerationResults.Add(new AiModerationResult(
                product.Id,
                seller.Id,
                AiModerationRiskLevel.High,
                needsAdminReview: true,
                "Potential counterfeit wording detected.",
                "[\"designer inspired\",\"mirror quality\"]",
                "[]",
                "[\"counterfeit-risk\"]",
                "local-rule-engine",
                DateTimeOffset.UtcNow));
        }

        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<VariantRevisionSeed> CreatePublishedProductWithVariantRevisionAsync(
        AdminProductReviewTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Variant Review Seller",
            "variant-review-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Variant Review Trading");

        var storefront = new SellerStorefront(seller.Id, "Variant Review Seller", $"variant-review-{Guid.NewGuid():N}");
        var address = new SellerAddress(
            seller.Id,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-456");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            "Published Variant Dress",
            $"published-variant-dress-{Guid.NewGuid():N}",
            "A live published product.",
            "A live published product with variant revisions.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        var variant = new ProductVariant(
            product.Id,
            "LIVE-SKU-M-BLACK",
            "M",
            "Black",
            499.99m,
            699.99m,
            10);
        var revision = new ProductVariantRevision(product.Id, seller.Id);
        var revisionItem = new ProductVariantRevisionItem(
            revision.Id,
            ProductVariantRevisionItemOperation.Update,
            variant.Id,
            "UPDATED-SKU-M-BLACK",
            "M",
            "Black",
            599.99m,
            799.99m,
            null,
            ProductVariantStatus.Active,
            "BAR-UPDATED");
        revision.UpdateSellerReason("Corrected retail price.");
        revision.SubmitForReview(hasAtLeastOneItem: true, DateTimeOffset.UtcNow);

        var buyer = new BuyerProfile(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            seller.Id,
            product.Title,
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            1,
            variant.AvailableQuantity);
        var cartItemId = cart.Items.Single().Id;

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        dbContext.Products.Add(product);
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "size", "\"M\""));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "colour", "\"Black\""));
        dbContext.ProductVariants.Add(variant);
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            "https://example.test/published-variant-dress.jpg",
            $"products/{product.Id:N}/primary.jpg",
            "Published variant dress",
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));
        dbContext.ProductVariantRevisions.Add(revision);
        dbContext.ProductVariantRevisionItems.Add(revisionItem);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();
        return new VariantRevisionSeed(product.Id, variant.Id, revision.Id, cartItemId);
    }

    private static async Task<Guid> CreatePendingListingRevisionAsync(
        AdminProductReviewTestFactory factory,
        Guid productId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var product = await dbContext.Products.SingleAsync(item => item.Id == productId);
        var revision = new ProductListingRevision(product.Id, product.SellerId);
        revision.UpdateProposal(
            product.CategoryId,
            null,
            $"{product.Title} Editorial Update",
            $"{product.Slug}-editorial-update",
            product.ShortDescription,
            product.FullDescription,
            "[]",
            "{}");
        revision.SubmitForReview(hasAtLeastOneImage: true, DateTimeOffset.UtcNow);

        dbContext.ProductListingRevisions.Add(revision);
        await dbContext.SaveChangesAsync();
        return revision.Id;
    }

    private sealed record VariantRevisionSeed(
        Guid ProductId,
        Guid VariantId,
        Guid RevisionId,
        Guid CartItemId);

    private sealed class AdminProductReviewTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminProductReviewTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
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
}
