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
using Swyftly.Api.Authentication;
using Swyftly.Api.Buyers;
using Swyftly.Api.Carts;
using Swyftly.Api.Orders;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Application.Orders;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public class OrderTests
{
    private const string TestPassword = "Password123!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task BuyerCanCreateOrderAndSellerCanReadIt()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await AuthorizeAsync(sellerClient, "seller-orders@example.test", SwyftlyRoles.Seller);
        var sellerId = await GetSellerProfileIdAsync(factory, sellerAuth.UserId);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Cotton Dress", 499m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-orders@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 2));
        addCartResponse.EnsureSuccessStatusCode();

        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));

        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(75m, order.ShippingAmount);
        Assert.Equal(1073m, order.TotalAmount);
        Assert.Equal(deliveryMethodId, order.DeliveryMethodId);
        Assert.Equal("Standard courier", order.DeliveryMethodName);
        Assert.Equal("Thabo Buyer", order.DeliveryAddress!.RecipientName);
        Assert.Equal("Leave with reception if needed.", order.DeliveryAddress.DeliveryInstructions);
        Assert.Single(order.Items);
        Assert.Single(order.StatusHistory);

        using var buyerOrdersResponse = await buyerClient.GetAsync("/api/orders");
        buyerOrdersResponse.EnsureSuccessStatusCode();
        var buyerOrders = await ReadJsonAsync<OrderResult[]>(buyerOrdersResponse);
        Assert.Contains(buyerOrders, savedOrder => savedOrder.OrderId == order.OrderId);

        using var sellerOrderResponse = await sellerClient.GetAsync($"/api/seller/orders/{order.OrderId}");
        sellerOrderResponse.EnsureSuccessStatusCode();
        var sellerOrder = await ReadJsonAsync<OrderResult>(sellerOrderResponse);
        Assert.Equal(order.OrderId, sellerOrder.OrderId);
        Assert.Equal(sellerId, sellerOrder.SellerId);
    }

    [Fact]
    public async Task BuyerCannotReadAnotherBuyersOrder()
    {
        await using var factory = new OrderTestFactory();
        using var firstBuyerClient = factory.CreateClient();
        using var secondBuyerClient = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Cotton Dress", 499m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(firstBuyerClient, "first-buyer-orders@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await firstBuyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();
        using var orderResponse = await firstBuyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        await AuthorizeAsync(secondBuyerClient, "second-buyer-orders@example.test", SwyftlyRoles.Buyer);

        using var forbiddenResponse = await secondBuyerClient.GetAsync($"/api/orders/{order.OrderId}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task BuyerCanCreateOrderWithSavedAddressSnapshot()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Saved Address Dress", 499m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-saved-address-order@example.test", SwyftlyRoles.Buyer);
        using var addressResponse = await buyerClient.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            TestSavedAddress("Home", "Original Recipient"));
        addressResponse.EnsureSuccessStatusCode();
        var address = await ReadJsonAsync<BuyerDeliveryAddressResponse>(addressResponse);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();

        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, address.DeliveryAddressId, null, deliveryMethodId));

        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        Assert.Equal("Original Recipient", order.DeliveryAddress!.RecipientName);
        Assert.Equal("Leave with reception if needed.", order.DeliveryAddress.DeliveryInstructions);

        using var updateResponse = await buyerClient.PutAsJsonAsync(
            $"/api/buyer/delivery-addresses/{address.DeliveryAddressId}",
            TestSavedAddress("Home", "Updated Recipient", "Ring the buzzer."));
        updateResponse.EnsureSuccessStatusCode();
        using var deleteResponse = await buyerClient.DeleteAsync($"/api/buyer/delivery-addresses/{address.DeliveryAddressId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getOrderResponse = await buyerClient.GetAsync($"/api/buyer/orders/{order.OrderId}");
        getOrderResponse.EnsureSuccessStatusCode();
        var savedOrder = await ReadJsonAsync<OrderResult>(getOrderResponse);
        Assert.Equal("Original Recipient", savedOrder.DeliveryAddress!.RecipientName);
        Assert.Equal("Leave with reception if needed.", savedOrder.DeliveryAddress.DeliveryInstructions);
    }

    [Fact]
    public async Task CreateOrderFromCart_RejectsMissingBothOrOtherBuyerDeliveryAddress()
    {
        await using var factory = new OrderTestFactory();
        using var firstBuyerClient = factory.CreateClient();
        using var secondBuyerClient = factory.CreateClient();
        var sellerId = await CreateSellerAsync(factory);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Address Guard Dress", 499m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(firstBuyerClient, "first-address-guard@example.test", SwyftlyRoles.Buyer);
        using var addressResponse = await firstBuyerClient.PostAsJsonAsync(
            "/api/buyer/delivery-addresses",
            TestSavedAddress("Home", "First Buyer"));
        addressResponse.EnsureSuccessStatusCode();
        var address = await ReadJsonAsync<BuyerDeliveryAddressResponse>(addressResponse);
        await AuthorizeAsync(secondBuyerClient, "second-address-guard@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await secondBuyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();

        using var missingResponse = await secondBuyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null));
        using var bothResponse = await secondBuyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, address.DeliveryAddressId, TestDeliveryAddress(), deliveryMethodId));
        using var otherBuyerAddressResponse = await secondBuyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, address.DeliveryAddressId, null, deliveryMethodId));

        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bothResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherBuyerAddressResponse.StatusCode);
    }

    [Fact]
    public async Task SellerCanManageFulfillmentAndBuyerCanTrackShipment()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await AuthorizeAsync(sellerClient, "seller-fulfillment@example.test", SwyftlyRoles.Seller);
        var sellerId = await GetSellerProfileIdAsync(factory, sellerAuth.UserId);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Linen Shirt", 300m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-fulfillment@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();
        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        await MarkOrderPaidAsync(factory, order.OrderId);

        using var processingResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-processing",
            content: null);
        processingResponse.EnsureSuccessStatusCode();
        var processingOrder = await ReadJsonAsync<OrderResult>(processingResponse);
        Assert.Equal("Processing", processingOrder.Status);
        Assert.Single(processingOrder.Shipments);

        using var readyResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-ready-to-ship",
            content: null);
        readyResponse.EnsureSuccessStatusCode();
        var readyOrder = await ReadJsonAsync<OrderResult>(readyResponse);
        Assert.Equal("ReadyToShip", readyOrder.Status);
        Assert.Equal("ReadyForCourier", Assert.Single(readyOrder.Shipments).Status);

        using var trackingResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/orders/{order.OrderId}/tracking",
            new AddOrderTrackingApiRequest("Courier One", "TRACK-123", "https://tracking.example/TRACK-123", "Collected by courier."));
        trackingResponse.EnsureSuccessStatusCode();

        using var shippedResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-shipped",
            content: null);
        shippedResponse.EnsureSuccessStatusCode();
        var shippedOrder = await ReadJsonAsync<OrderResult>(shippedResponse);
        Assert.Equal("Shipped", shippedOrder.Status);
        var shipment = Assert.Single(shippedOrder.Shipments);
        Assert.Equal("InTransit", shipment.Status);
        Assert.Equal("TRACK-123", shipment.TrackingNumber);
        Assert.Contains(shipment.Events, shipmentEvent => shipmentEvent.EventType == "TrackingUpdated");
        Assert.Contains(shipment.Events, shipmentEvent => shipmentEvent.EventType == "ShipmentInTransit");

        using var deliveredResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivered",
            content: null);
        deliveredResponse.EnsureSuccessStatusCode();
        var deliveredOrder = await ReadJsonAsync<OrderResult>(deliveredResponse);
        Assert.Equal("Delivered", deliveredOrder.Status);
        var deliveredShipment = Assert.Single(deliveredOrder.Shipments);
        Assert.Equal("Delivered", deliveredShipment.Status);
        Assert.NotNull(deliveredShipment.DeliveredAtUtc);
        Assert.Contains(deliveredShipment.Events, shipmentEvent => shipmentEvent.EventType == "ShipmentDelivered");

        using var duplicateDeliveredResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivered",
            content: null);
        duplicateDeliveredResponse.EnsureSuccessStatusCode();
        var duplicateDeliveredOrder = await ReadJsonAsync<OrderResult>(duplicateDeliveredResponse);
        Assert.Equal("Delivered", duplicateDeliveredOrder.Status);

        using var buyerTrackResponse = await buyerClient.GetAsync($"/api/buyer/orders/{order.OrderId}");
        buyerTrackResponse.EnsureSuccessStatusCode();
        var trackedOrder = await ReadJsonAsync<OrderResult>(buyerTrackResponse);
        Assert.Equal("Delivered", trackedOrder.Status);
        Assert.Equal("TRACK-123", Assert.Single(trackedOrder.Shipments).TrackingNumber);

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await ReadJsonAsync<NotificationResult[]>(notificationsResponse);
        Assert.Contains(notifications, notification => notification.Type == "OrderTrackingAdded" && notification.RelatedEntityId == order.OrderId);
        Assert.Contains(notifications, notification => notification.Type == "OrderReadyToShip" && notification.RelatedEntityId == order.OrderId);
        Assert.Contains(notifications, notification => notification.Type == "OrderShipped" && notification.RelatedEntityId == order.OrderId);
        Assert.Single(notifications, notification => notification.Type == "OrderDelivered" && notification.RelatedEntityId == order.OrderId);
    }

    [Fact]
    public async Task SellerCanRecordDeliveryFailureAndReturnedToSenderWithoutChangingOrderStatus()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await AuthorizeAsync(sellerClient, "seller-delivery-exception@example.test", SwyftlyRoles.Seller);
        var sellerId = await GetSellerProfileIdAsync(factory, sellerAuth.UserId);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Delivery Exception Shirt", 300m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-delivery-exception@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();
        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        await MarkOrderPaidAsync(factory, order.OrderId);
        using var shippedResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-shipped",
            content: null);
        shippedResponse.EnsureSuccessStatusCode();

        using var failedResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivery-failed",
            new FulfillmentExceptionApiRequest("Courier could not reach the recipient."));
        failedResponse.EnsureSuccessStatusCode();
        var failedOrder = await ReadJsonAsync<OrderResult>(failedResponse);
        Assert.Equal("Shipped", failedOrder.Status);
        Assert.Equal("DeliveryFailed", Assert.Single(failedOrder.Shipments).Status);
        Assert.Contains(Assert.Single(failedOrder.Shipments).Events, shipmentEvent => shipmentEvent.EventType == "DeliveryFailed");

        using var duplicateFailedResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivery-failed",
            new FulfillmentExceptionApiRequest("Courier could not reach the recipient."));
        duplicateFailedResponse.EnsureSuccessStatusCode();

        using var returnedResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/orders/{order.OrderId}/mark-returned-to-sender",
            new FulfillmentExceptionApiRequest("Parcel returned by courier."));
        returnedResponse.EnsureSuccessStatusCode();
        var returnedOrder = await ReadJsonAsync<OrderResult>(returnedResponse);
        Assert.Equal("Shipped", returnedOrder.Status);
        Assert.Equal("ReturnedToSender", Assert.Single(returnedOrder.Shipments).Status);
        Assert.Contains(Assert.Single(returnedOrder.Shipments).Events, shipmentEvent => shipmentEvent.EventType == "ReturnedToSender");

        using var deliveredResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivered",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, deliveredResponse.StatusCode);

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await ReadJsonAsync<NotificationResult[]>(notificationsResponse);
        Assert.Contains(notifications, notification => notification.Type == "OrderDeliveryFailed" && notification.RelatedEntityId == order.OrderId);
        Assert.Contains(notifications, notification => notification.Type == "OrderReturnedToSender" && notification.RelatedEntityId == order.OrderId);
    }

    [Fact]
    public async Task SellerCannotMarkPaidOrderDeliveredBeforeShipment()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await AuthorizeAsync(sellerClient, "seller-delivery-invalid@example.test", SwyftlyRoles.Seller);
        var sellerId = await GetSellerProfileIdAsync(factory, sellerAuth.UserId);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Invalid Delivery Dress", 300m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-delivery-invalid@example.test", SwyftlyRoles.Buyer);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();
        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        await MarkOrderPaidAsync(factory, order.OrderId);

        using var deliveredResponse = await sellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-delivered",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, deliveredResponse.StatusCode);
    }

    [Fact]
    public async Task SellerCannotManageAnotherSellersOrderFulfillment()
    {
        await using var factory = new OrderTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        using var otherSellerClient = factory.CreateClient();
        var sellerAuth = await AuthorizeAsync(sellerClient, "seller-owned-order@example.test", SwyftlyRoles.Seller);
        var sellerId = await GetSellerProfileIdAsync(factory, sellerAuth.UserId);
        var variantId = await CreatePublishedProductAsync(factory, sellerId, "Denim Jacket", 700m);
        var deliveryMethodId = await GetDeliveryMethodIdAsync(factory, sellerId);
        await AuthorizeAsync(buyerClient, "buyer-owned-order@example.test", SwyftlyRoles.Buyer);
        await AuthorizeAsync(otherSellerClient, "seller-other-order@example.test", SwyftlyRoles.Seller);
        using var addCartResponse = await buyerClient.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(variantId, 1));
        addCartResponse.EnsureSuccessStatusCode();
        using var orderResponse = await buyerClient.PostAsJsonAsync(
            "/api/orders/from-cart",
            new CreateOrderFromCartApiRequest(null, null, DeliveryAddress: TestDeliveryAddress(), DeliveryMethodId: deliveryMethodId));
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadJsonAsync<OrderResult>(orderResponse);
        await MarkOrderPaidAsync(factory, order.OrderId);

        using var forbiddenResponse = await otherSellerClient.PostAsync(
            $"/api/seller/orders/{order.OrderId}/mark-processing",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
    }

    private static async Task<AuthResponse> AuthorizeAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await ReadJsonAsync<AuthResponse>(loginResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private static async Task<Guid> GetSellerProfileIdAsync(OrderTestFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == userId)
            .Select(seller => seller.Id)
            .SingleAsync();
    }

    private static async Task<Guid> GetDeliveryMethodIdAsync(OrderTestFactory factory, Guid sellerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var existing = await dbContext.SellerDeliveryMethods
            .Where(method => method.SellerId == sellerId)
            .Select(method => (Guid?)method.Id)
            .FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var method = new SellerDeliveryMethod(
            sellerId,
            "Standard courier",
            "Door-to-door delivery within South Africa.",
            SellerDeliveryMethodType.Standard,
            "ZA",
            "Gauteng",
            75m,
            null,
            2,
            5,
            10,
            isActive: true);

        dbContext.SellerDeliveryMethods.Add(method);
        await dbContext.SaveChangesAsync();
        return method.Id;
    }

    private static async Task<Guid> CreateSellerAsync(OrderTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Seller One",
            "seller-one@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Seller One Trading");
        var storefront = new SellerStorefront(seller.Id, "Seller One", "seller-one");
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

    private static async Task MarkOrderPaidAsync(OrderTestFactory factory, Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var order = await dbContext.Orders
            .Include(existing => existing.StatusHistory)
            .SingleAsync(existing => existing.Id == orderId);
        order.ChangeStatus(OrderStatus.Paid, DateTimeOffset.UtcNow, "TestPaymentConfirmed");
        dbContext.OrderStatusHistory.Add(order.StatusHistory.Last());
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> CreatePublishedProductAsync(
        OrderTestFactory factory,
        Guid sellerId,
        string title,
        decimal price)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            title,
            title.ToLowerInvariant().Replace(' ', '-'),
            "A marketplace-ready item.",
            "A published item for order testing.");
        product.UpdateTags("[\"order\"]");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);
        var variant = new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            price,
            price + 100,
            stockQuantity: 10);

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync();
        return variant.Id;
    }

    private static CreateOrderDeliveryAddressApiRequest TestDeliveryAddress(string recipientName = "Thabo Buyer") =>
        new(
            recipientName,
            "+27110000000",
            "10 Market Street",
            "Apartment 4",
            "Rosebank",
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA",
            "Leave with reception if needed.");

    private static BuyerDeliveryAddressRequest TestSavedAddress(
        string label,
        string recipientName,
        string deliveryInstructions = "Leave with reception if needed.") =>
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
            "ZA",
            true,
            deliveryInstructions);

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Response body could not be deserialized as {typeof(T).Name}.");
    }

    private sealed class OrderTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyOrderTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();

                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
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
