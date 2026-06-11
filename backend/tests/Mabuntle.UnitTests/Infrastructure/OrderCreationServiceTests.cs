using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mabuntle.Application.Analytics;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Inventory;
using Mabuntle.Application.Orders;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Delivery;
using Mabuntle.Infrastructure.Orders;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class OrderCreationServiceTests
{
    [Fact]
    public async Task CreateFromCartAsync_CreatesPendingPaymentOrderAndSnapshotsCartItems()
    {
        await using var dbContext = CreateDbContext();
        var (buyer, product, variant, cart, deliveryMethod) = await SeedCartAsync(dbContext, price: 499m, quantity: 2);
        var service = CreateService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var result = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt,
            TimeSpan.FromMinutes(15),
            DeliveryAddress: TestDeliveryAddress(),
            DeliveryMethodId: deliveryMethod.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingPayment", result.Value.Status);
        Assert.Equal(cart.Id, result.Value.CartId);
        Assert.Equal(product.SellerId, result.Value.SellerId);
        Assert.Equal(75m, result.Value.ShippingAmount);
        Assert.Equal(1073m, result.Value.TotalAmount);
        Assert.Equal(deliveryMethod.Id, result.Value.DeliveryMethodId);
        Assert.Equal("Standard courier", result.Value.DeliveryMethodName);
        Assert.Equal("Standard", result.Value.DeliveryMethodType);
        Assert.Equal("Leave with reception if needed.", result.Value.DeliveryAddress!.DeliveryInstructions);
        Assert.NotNull(result.Value.SellerPolicySnapshot);
        Assert.Equal(14, result.Value.SellerPolicySnapshot!.ReturnWindowDays);
        Assert.Equal("Returns are reviewed for delivered items in original condition.", result.Value.SellerPolicySnapshot.ReturnPolicy);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal("Cotton Dress", item.ProductTitle);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(499m, item.UnitPrice);
        Assert.Equal(998m, item.LineTotal);
        Assert.Single(result.Value.StatusHistory);

        var reservation = await dbContext.InventoryReservations.SingleAsync();
        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);
        Assert.Equal(2, variant.ReservedQuantity);
        Assert.Equal(CartStatus.CheckedOut, cart.Status);
    }

    [Fact]
    public async Task CreateFromCartAsync_ReturnsExistingPendingPaymentOrderForSameCart()
    {
        await using var dbContext = CreateDbContext();
        var (buyer, _, _, cart, deliveryMethod) = await SeedCartAsync(dbContext, price: 499m, quantity: 1);
        var service = CreateService(dbContext);
        var startedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var first = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt,
            TimeSpan.FromMinutes(15),
            DeliveryAddress: TestDeliveryAddress(),
            DeliveryMethodId: deliveryMethod.Id));

        var second = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            startedAt.AddMinutes(1),
            TimeSpan.FromMinutes(15),
            DeliveryAddress: TestDeliveryAddress("Second recipient"),
            DeliveryMethodId: deliveryMethod.Id));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.OrderId, second.Value.OrderId);
        Assert.Equal(1, await dbContext.Orders.CountAsync());
        Assert.Equal(1, await dbContext.InventoryReservations.CountAsync());
        Assert.Equal(CartStatus.CheckedOut, cart.Status);
    }

    [Fact]
    public async Task CreateFromCartAsync_ReturnsValidationFailureForEmptyCart()
    {
        await using var dbContext = CreateDbContext();
        var buyer = new BuyerProfile(Guid.NewGuid());
        var cart = new Cart(buyer.Id);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.CreateFromCartAsync(new CreateOrderFromCartRequest(
            buyer.Id,
            cart.Id,
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
            TimeSpan.FromMinutes(15),
            DeliveryAddress: TestDeliveryAddress(),
            DeliveryMethodId: Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Contains("at least one item", result.Error.Details!["cart"].Single());
    }

    private static EfOrderCreationService CreateService(MabuntleDbContext dbContext) =>
        new(
            dbContext,
            new EfInventoryReservationService(dbContext),
            new LocalRulesAddressVerificationService(TimeProvider.System),
            new NoOpStorefrontAnalyticsService(),
            new NoOpBuyerGrowthOutcomeAttributionService());

    private static OrderDeliveryAddressRequest TestDeliveryAddress(string recipientName = "Thabo Buyer") =>
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

    private sealed class NoOpStorefrontAnalyticsService : IStorefrontAnalyticsService
    {
        public Task<Result<StorefrontFunnelEventResult>> RecordClientEventAsync(
            StorefrontFunnelEventRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StorefrontFunnelEventResult>.Success(new StorefrontFunnelEventResult(false, null, "Skipped")));

        public Task RecordOrderCreatedAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpBuyerGrowthOutcomeAttributionService : IBuyerGrowthOutcomeAttributionService
    {
        public Task RecordProductOpenedAsync(
            Guid buyerId,
            Guid productId,
            Guid sourceEventId,
            BuyerGrowthSourceTool sourceTool,
            BuyerGrowthConfidenceBand? confidenceBand,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordProductAddedToCartAsync(
            Guid buyerId,
            Guid productId,
            Guid cartId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordCheckoutStartedAsync(
            Guid buyerId,
            Guid cartId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOrderCreatedAsync(
            Guid orderId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOrderPaidAsync(
            Guid orderId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static async Task<(BuyerProfile Buyer, Product Product, ProductVariant Variant, Cart Cart, SellerDeliveryMethod DeliveryMethod)> SeedCartAsync(
        MabuntleDbContext dbContext,
        decimal price,
        int quantity)
    {
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var deliveryMethod = new SellerDeliveryMethod(
            product.SellerId,
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
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", price, price + 100, stockQuantity: 5);
        var storePolicy = new SellerStorePolicy(
            product.SellerId,
            14,
            "Returns are reviewed for delivered items in original condition.",
            "Exchanges depend on stock availability.",
            "Orders are usually dispatched within 2-3 business days.",
            "Message support with order issues and product questions.",
            "Follow product care notes on each item.",
            "Colour and fit may vary slightly by screen and size.");
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(
            product.Id,
            variant.Id,
            product.SellerId,
            "Cotton Dress",
            variant.Sku,
            variant.Size,
            variant.Colour,
            variant.Price,
            quantity,
            variant.AvailableQuantity);

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.SellerDeliveryMethods.Add(deliveryMethod);
        dbContext.SellerStorePolicies.Add(storePolicy);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
        return (buyer, product, variant, cart, deliveryMethod);
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"OrderCreationServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new MabuntleDbContext(options);
    }
}
