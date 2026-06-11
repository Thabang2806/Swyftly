using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Carts;
using Mabuntle.Api.Sellers;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Delivery;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public class CartTests
{
    private const string TestPassword = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Buyer_CanAddAndReadCartItem()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "buyer@example.test");
        var variantId = await CreatePublishedProductAsync(factory, await CreateSellerAsync(factory, "Seller One", "seller-one"), "Cotton Dress", 499m);

        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 2));

        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);
        var item = Assert.Single(cart.Items);
        Assert.Equal(variantId, item.ProductVariantId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(499m, item.UnitPrice);
        Assert.Equal("cotton-dress", item.ProductSlug);
        Assert.Equal("https://example.test/cotton-dress.jpg", item.PrimaryImageUrl);
        Assert.Equal(998m, cart.Subtotal);

        using var getResponse = await client.GetAsync("/api/cart");
        getResponse.EnsureSuccessStatusCode();
        var savedCart = await ReadJsonAsync<CartResponse>(getResponse);
        Assert.Equal(cart.CartId, savedCart.CartId);
        Assert.Single(savedCart.Items);
    }

    [Fact]
    public async Task Buyer_CanAddMultipleItemsFromSameSellerToExistingCart()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "multi-item-buyer@example.test");
        var sellerId = await CreateSellerAsync(factory, "Seller One", "seller-one");
        var firstVariantId = await CreatePublishedProductAsync(factory, sellerId, "Cotton Dress", 499m);
        var secondVariantId = await CreatePublishedProductAsync(factory, sellerId, "Silk Blouse", 699m);
        using var firstResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(firstVariantId, 1));
        firstResponse.EnsureSuccessStatusCode();

        using var secondResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(secondVariantId, 1));

        secondResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(secondResponse);
        Assert.Equal(2, cart.Items.Count);
        Assert.Contains(cart.Items, item => item.ProductVariantId == firstVariantId);
        Assert.Contains(cart.Items, item => item.ProductVariantId == secondVariantId);
    }

    [Fact]
    public async Task Buyer_CanMoveCartItemToWishlist()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "move-cart-buyer@example.test");
        var variantId = await CreatePublishedProductAsync(factory, await CreateSellerAsync(factory, "Seller One", "seller-one"), "Cotton Dress", 499m);
        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);
        var item = Assert.Single(cart.Items);

        using var moveResponse = await client.PostAsync($"/api/cart/items/{item.CartItemId}/move-to-wishlist", null);

        moveResponse.EnsureSuccessStatusCode();
        var updatedCart = await ReadJsonAsync<CartResponse>(moveResponse);
        Assert.Empty(updatedCart.Items);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.True(await dbContext.BuyerWishlistItems.AnyAsync(wishlist => wishlist.ProductId == item.ProductId));
    }

    [Fact]
    public async Task Buyer_CanGetShippingOptionsForCartAddress()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "shipping-buyer@example.test");
        var sellerId = await CreateSellerAsync(factory, "Seller One", "seller-one");
        await CreateDeliveryMethodAsync(factory, sellerId, "Standard courier", SellerDeliveryMethodType.Standard, null, 75m, freeShippingThreshold: 1000m);
        await CreateDeliveryMethodAsync(factory, sellerId, "Gauteng local courier", SellerDeliveryMethodType.LocalCourier, "Gauteng", 45m, freeShippingThreshold: null);
        await CreateDeliveryMethodAsync(factory, sellerId, "Inactive express", SellerDeliveryMethodType.Express, "Gauteng", 120m, freeShippingThreshold: null, isActive: false);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Shipping Dress", 1200m);
        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);

        using var response = await client.PostAsJsonAsync(
            "/api/cart/shipping-options",
            new CartShippingOptionsRequest(cart.CartId, DeliveryAddress: TestDeliveryAddress()));

        response.EnsureSuccessStatusCode();
        var options = await ReadJsonAsync<CartShippingOptionsResponse>(response);
        Assert.Equal(cart.CartId, options.CartId);
        Assert.Equal(1200m, options.CartSubtotal);
        Assert.Equal(2, options.Options.Count);
        Assert.Contains(options.Options, option => option.Name == "Standard courier" && option.ShippingAmount == 0m && option.FreeShippingApplied);
        Assert.Contains(options.Options, option => option.Name == "Gauteng local courier" && option.ShippingAmount == 45m);
    }

    [Fact]
    public async Task Buyer_ShippingOptionsIncludePickupPointsForPickupDeliveryMethod()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "shipping-pickup-buyer@example.test");
        var sellerId = await CreateSellerAsync(factory, "Seller One", "seller-one");
        await CreateDeliveryMethodAsync(factory, sellerId, "Pickup counter", SellerDeliveryMethodType.PickupPoint, "Gauteng", 25m, freeShippingThreshold: null);
        await CreatePickupPointAsync(factory, "Manual", "JHB-ROSEBANK-001", "Rosebank Pickup Counter", "Gauteng", isActive: true);
        await CreatePickupPointAsync(factory, "Manual", "CPT-SEA-001", "Sea Point Pickup Counter", "Western Cape", isActive: true);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Pickup Dress", 499m);
        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);

        using var response = await client.PostAsJsonAsync(
            "/api/cart/shipping-options",
            new CartShippingOptionsRequest(cart.CartId, DeliveryAddress: TestDeliveryAddress()));

        response.EnsureSuccessStatusCode();
        var options = await ReadJsonAsync<CartShippingOptionsResponse>(response);
        var option = Assert.Single(options.Options);
        Assert.Equal("PickupPoint", option.MethodType);
        Assert.True(option.RequiresPickupPoint);
        var pickupPoint = Assert.Single(option.PickupPoints!);
        Assert.Equal("Rosebank Pickup Counter", pickupPoint.Name);
        Assert.Equal("Verified", options.AddressVerification!.VerificationStatus);
    }

    [Fact]
    public async Task Buyer_ShippingOptionsRejectsNoMatchingSellerMethod()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "shipping-no-match-buyer@example.test");
        var sellerId = await CreateSellerAsync(factory, "Seller One", "seller-one");
        await CreateDeliveryMethodAsync(factory, sellerId, "Cape courier", SellerDeliveryMethodType.Standard, "Western Cape", 75m, freeShippingThreshold: null);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Shipping Guard Dress", 499m);
        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);

        using var response = await client.PostAsJsonAsync(
            "/api/cart/shipping-options",
            new CartShippingOptionsRequest(cart.CartId, DeliveryAddress: TestDeliveryAddress()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CanManageDeliveryMethods()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeSellerAsync(client, "delivery-method-seller@example.test");

        using var createResponse = await client.PostAsJsonAsync(
            "/api/seller/delivery-methods",
            new SellerDeliveryMethodRequest(
                "Standard courier",
                "Door-to-door delivery within South Africa.",
                "Standard",
                "ZA",
                "Gauteng",
                75m,
                1000m,
                2,
                5,
                10,
                true));
        createResponse.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync<SellerDeliveryMethodResponse>(createResponse);
        Assert.Equal("Standard courier", created.Name);
        Assert.True(created.IsActive);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/seller/delivery-methods/{created.DeliveryMethodId}",
            new SellerDeliveryMethodRequest(
                "Express courier",
                "Faster door-to-door delivery.",
                "Express",
                "ZA",
                null,
                120m,
                null,
                1,
                2,
                5,
                true));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await ReadJsonAsync<SellerDeliveryMethodResponse>(updateResponse);
        Assert.Equal("Express", updated.MethodType);
        Assert.Null(updated.Province);

        using var deactivateResponse = await client.PostAsync($"/api/seller/delivery-methods/{created.DeliveryMethodId}/deactivate", null);
        deactivateResponse.EnsureSuccessStatusCode();
        Assert.False((await ReadJsonAsync<SellerDeliveryMethodResponse>(deactivateResponse)).IsActive);

        using var activateResponse = await client.PostAsync($"/api/seller/delivery-methods/{created.DeliveryMethodId}/activate", null);
        activateResponse.EnsureSuccessStatusCode();
        Assert.True((await ReadJsonAsync<SellerDeliveryMethodResponse>(activateResponse)).IsActive);

        using var listResponse = await client.GetAsync("/api/seller/delivery-methods");
        listResponse.EnsureSuccessStatusCode();
        Assert.Single(await ReadJsonAsync<SellerDeliveryMethodResponse[]>(listResponse));
    }

    [Fact]
    public async Task Buyer_CannotMixSellersInCart()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "buyer@example.test");
        var firstVariantId = await CreatePublishedProductAsync(factory, await CreateSellerAsync(factory, "Seller One", "seller-one"), "Cotton Dress", 499m);
        var secondVariantId = await CreatePublishedProductAsync(factory, await CreateSellerAsync(factory, "Seller Two", "seller-two"), "Leather Shoes", 799m);
        using var firstResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(firstVariantId, 1));
        firstResponse.EnsureSuccessStatusCode();

        using var mixedSellerResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(secondVariantId, 1));

        Assert.Equal(HttpStatusCode.BadRequest, mixedSellerResponse.StatusCode);
        var body = await mixedSellerResponse.Content.ReadAsStringAsync();
        Assert.Contains("only one seller", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Buyer_CannotAddQuantityAboveAvailableStock()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "buyer@example.test");
        var variantId = await CreatePublishedProductAsync(
            factory,
            await CreateSellerAsync(factory, "Seller One", "seller-one"),
            "Cotton Dress",
            499m,
            stockQuantity: 5,
            reservedQuantity: 3);

        using var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 3));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("available stock", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Buyer_CanUpdateAndDeleteCartItem()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeBuyerAsync(client, "buyer@example.test");
        var variantId = await CreatePublishedProductAsync(factory, await CreateSellerAsync(factory, "Seller One", "seller-one"), "Cotton Dress", 499m);
        using var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addResponse.EnsureSuccessStatusCode();
        var cart = await ReadJsonAsync<CartResponse>(addResponse);
        var itemId = Assert.Single(cart.Items).CartItemId;

        using var updateResponse = await client.PutAsJsonAsync($"/api/cart/items/{itemId}", new UpdateCartItemRequest(3));

        updateResponse.EnsureSuccessStatusCode();
        var updatedCart = await ReadJsonAsync<CartResponse>(updateResponse);
        Assert.Equal(3, Assert.Single(updatedCart.Items).Quantity);

        using var deleteResponse = await client.DeleteAsync($"/api/cart/items/{itemId}");

        deleteResponse.EnsureSuccessStatusCode();
        var emptiedCart = await ReadJsonAsync<CartResponse>(deleteResponse);
        Assert.Empty(emptiedCart.Items);
        Assert.Null(emptiedCart.SellerId);
    }

    [Fact]
    public async Task Seller_CannotAccessBuyerCart()
    {
        await using var factory = new CartTestFactory();
        using var client = factory.CreateClient();
        await AuthorizeSellerAsync(client, "seller@example.test");

        using var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task AuthorizeBuyerAsync(HttpClient client, string email)
    {
        await RegisterAsync(client, email, MabuntleRoles.Buyer);
        var login = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private static async Task AuthorizeSellerAsync(HttpClient client, string email)
    {
        await RegisterAsync(client, email, MabuntleRoles.Seller);
        var login = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AuthResponse>(response);
    }

    private static async Task<Guid> CreateSellerAsync(
        CartTestFactory factory,
        string storeName,
        string storeSlug)
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
        storefront.Publish();
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        await dbContext.SaveChangesAsync();
        return seller.Id;
    }

    private static async Task<Guid> CreatePublishedProductAsync(
        CartTestFactory factory,
        Guid sellerId,
        string title,
        decimal price,
        int stockQuantity = 10,
        int reservedQuantity = 0)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            title,
            title.ToLowerInvariant().Replace(' ', '-'),
            "A marketplace-ready item.",
            "A published item for cart testing.");
        product.UpdateTags("[\"cart\"]");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        var variant = new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            price,
            price + 100,
            stockQuantity,
            reservedQuantity);

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            $"https://example.test/{product.Slug}.jpg",
            $"products/{product.Id:N}/primary.jpg",
            title,
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<Guid> CreateDeliveryMethodAsync(
        CartTestFactory factory,
        Guid sellerId,
        string name,
        SellerDeliveryMethodType methodType,
        string? province,
        decimal basePrice,
        decimal? freeShippingThreshold,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var method = new SellerDeliveryMethod(
            sellerId,
            name,
            null,
            methodType,
            "ZA",
            province,
            basePrice,
            freeShippingThreshold,
            2,
            5,
            10,
            isActive);
        dbContext.SellerDeliveryMethods.Add(method);
        await dbContext.SaveChangesAsync();
        return method.Id;
    }

    private static async Task<Guid> CreatePickupPointAsync(
        CartTestFactory factory,
        string providerName,
        string code,
        string name,
        string province,
        bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var point = new PickupPoint(
            providerName,
            code,
            name,
            "10 Market Street",
            null,
            "Rosebank",
            "Johannesburg",
            province,
            "2196",
            "ZA",
            null,
            null,
            "Mon-Fri 09:00-17:00",
            isActive);
        dbContext.PickupPoints.Add(point);
        await dbContext.SaveChangesAsync();
        return point.Id;
    }

    private static CartShippingDeliveryAddressRequest TestDeliveryAddress() =>
        new(
            "Thabo Buyer",
            "+27110000000",
            "10 Market Street",
            "Apartment 4",
            "Rosebank",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA",
            "Leave with reception if needed.");

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Response body could not be deserialized as {typeof(T).Name}.");
    }

    private sealed class CartTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleCartTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MabuntleDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MabuntleDbContext>>();

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
