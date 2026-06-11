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
using Mabuntle.Api.Sellers;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Notifications;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Domain.Support;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class SellerDashboardTests
{
    [Fact]
    public async Task Buyer_CannotAccessSellerDashboardSummary()
    {
        using var factory = new SellerDashboardTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "dashboard-buyer@example.test", MabuntleRoles.Buyer);
        var token = await LoginAsync(client, "dashboard-buyer@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await client.GetAsync("/api/seller/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnverifiedSeller_GetsConflict()
    {
        using var factory = new SellerDashboardTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "dashboard-unverified@example.test", verify: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.GetAsync("/api/seller/dashboard/summary");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task VerifiedSellerDashboard_ReturnsOnlySellerOwnedOperationalCounts()
    {
        using var factory = new SellerDashboardTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await CreateSellerUserAsync(factory, client, "dashboard-seller-one@example.test", verify: true);
        var sellerTwo = await CreateSellerUserAsync(factory, client, "dashboard-seller-two@example.test", verify: true);
        await SeedDashboardDataAsync(factory, sellerOne, sellerTwo);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);

        using var response = await client.GetAsync("/api/seller/dashboard/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<SellerDashboardSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(sellerOne.SellerId, summary!.SellerId);
        Assert.Equal(998m, summary.SalesLast30Days);
        Assert.Equal(4, summary.OrdersLast30Days);
        Assert.Equal(1, summary.PaidOrderCount);
        Assert.Equal(1, summary.ProcessingOrderCount);
        Assert.Equal(3, summary.PendingFulfilmentOrders);
        Assert.Equal(1, summary.DeliveryExceptionOrderCount);
        Assert.Equal(1, summary.PendingReviewProductCount);
        Assert.Equal(1, summary.ChangesRequestedProductCount);
        Assert.Equal(1, summary.PendingListingRevisionCount);
        Assert.Equal(1, summary.PendingVariantRevisionCount);
        Assert.Equal(1, summary.LowStockProductCount);
        Assert.Equal(1, summary.OutOfStockVariantCount);
        Assert.Equal(2, summary.ReservedStockCount);
        Assert.Equal(1, summary.OpenReturnCount);
        Assert.Equal(1, summary.ReturnsAwaitingSellerResponseCount);
        Assert.Equal(1, summary.OpenSupportTicketCount);
        Assert.Equal(1, summary.ActiveDisputeCount);
        Assert.Equal(3400m, summary.PendingPayoutAmount);
        Assert.Equal(1200m, summary.AvailablePayoutAmount);
        Assert.Equal(100m, summary.HeldPayoutAmount);
        Assert.True(summary.HasPendingPayoutProfileChange);
        Assert.Equal(1, summary.ActiveAdCampaignCount);
        Assert.Equal(1, summary.PendingAdReviewCount);
        Assert.Equal(25m, summary.AdSpendLast30Days);
        Assert.Equal(998m, summary.AdRevenueLast30Days);
        Assert.Equal(1, summary.UnreadNotificationCount);
        Assert.Contains(summary.Alerts, alert => alert.Title == "Delivery exceptions need review");
        Assert.Contains(summary.Alerts, alert => alert.Title == "Payout profile change pending");
        Assert.Contains(summary.RecentActivity, activity => activity.Type == "Order");
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<SellerAuthContext> CreateSellerUserAsync(
        SellerDashboardTestFactory factory,
        HttpClient client,
        string email,
        bool verify)
    {
        await RegisterAsync(client, email, MabuntleRoles.Seller);
        var auth = await LoginAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var seller = await dbContext.SellerProfiles.SingleAsync(profile => profile.UserId == user!.Id);
        if (verify)
        {
            VerifySeller(dbContext, seller);
            await dbContext.SaveChangesAsync();
        }

        return new SellerAuthContext(seller.Id, seller.UserId, auth.AccessToken);
    }

    private static void VerifySeller(MabuntleDbContext dbContext, SellerProfile seller)
    {
        seller.UpdateProfile(
            $"Seller {seller.Id:N}"[..24],
            $"seller-{seller.Id:N}@example.test",
            "+27110000000",
            SellerBusinessType.Individual,
            null);
        var storefront = new SellerStorefront(
            seller.Id,
            $"Store {seller.Id:N}"[..24],
            $"store-{seller.Id:N}"[..32]);
        storefront.Publish();
        var address = new SellerAddress(
            seller.Id,
            "10 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2196",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, $"provider-{seller.Id:N}");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!;
    }

    private static async Task SeedDashboardDataAsync(
        SellerDashboardTestFactory factory,
        SellerAuthContext sellerOne,
        SellerAuthContext sellerTwo)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;
        var buyerId = Guid.NewGuid();
        var sellerOneProduct = CreatePublishedProduct(sellerOne.SellerId, "Dashboard Product");
        var sellerTwoProduct = CreatePublishedProduct(sellerTwo.SellerId, "Other Seller Product");
        var sellerOneVariant = new ProductVariant(sellerOneProduct.Id, "DASH-1", "M", "Black", 499m, null, 4);
        var reservedVariant = new ProductVariant(sellerOneProduct.Id, "DASH-2", "L", "Black", 250m, null, 8, reservedQuantity: 2);
        var outOfStockVariant = new ProductVariant(
            sellerOneProduct.Id,
            "DASH-3",
            "S",
            "Black",
            199m,
            null,
            0,
            status: ProductVariantStatus.OutOfStock);
        var otherSellerVariant = new ProductVariant(sellerTwoProduct.Id, "OTHER-1", "M", "Black", 999m, null, 1);

        var paidOrder = new Order(buyerId, sellerOne.SellerId, Guid.NewGuid(), now.AddDays(-2));
        paidOrder.AddItem(
            sellerOneProduct.Id,
            sellerOneVariant.Id,
            sellerOneProduct.Title,
            sellerOneVariant.Sku,
            sellerOneVariant.Size,
            sellerOneVariant.Colour,
            sellerOneVariant.Price,
            2);
        paidOrder.ChangeStatus(OrderStatus.Paid, now.AddDays(-1), "PaymentConfirmed");
        var processingOrder = new Order(Guid.NewGuid(), sellerOne.SellerId, Guid.NewGuid(), now.AddDays(-3));
        processingOrder.ChangeStatus(OrderStatus.Processing, now.AddDays(-2), "Processing");
        var readyOrder = new Order(Guid.NewGuid(), sellerOne.SellerId, Guid.NewGuid(), now.AddDays(-4));
        readyOrder.ChangeStatus(OrderStatus.ReadyToShip, now.AddDays(-3), "Ready");
        var exceptionOrder = new Order(Guid.NewGuid(), sellerOne.SellerId, Guid.NewGuid(), now.AddDays(-5));
        exceptionOrder.ChangeStatus(OrderStatus.Shipped, now.AddDays(-4), "Shipped");
        var exceptionShipment = new Shipment(exceptionOrder.Id, sellerOne.SellerId, exceptionOrder.BuyerId, now.AddDays(-4));
        exceptionShipment.MarkInTransit(now.AddDays(-4));
        exceptionShipment.MarkDeliveryFailed("Recipient unavailable.", now.AddDays(-3));

        var otherSellerOrder = new Order(Guid.NewGuid(), sellerTwo.SellerId, Guid.NewGuid(), now.AddDays(-2));
        otherSellerOrder.AddItem(
            sellerTwoProduct.Id,
            otherSellerVariant.Id,
            sellerTwoProduct.Title,
            otherSellerVariant.Sku,
            otherSellerVariant.Size,
            otherSellerVariant.Colour,
            otherSellerVariant.Price,
            1);
        otherSellerOrder.ChangeStatus(OrderStatus.Paid, now.AddDays(-1), "PaymentConfirmed");

        var pendingProduct = CreateReviewProduct(sellerOne.SellerId, "Pending Dashboard Product");
        var changesProduct = CreateReviewProduct(sellerOne.SellerId, "Changes Dashboard Product");
        changesProduct.RequestChanges("Improve imagery.");
        var listingRevision = new ProductListingRevision(sellerOneProduct.Id, sellerOne.SellerId);
        listingRevision.UpdateProposal(
            sellerOneProduct.CategoryId,
            null,
            sellerOneProduct.Title,
            sellerOneProduct.Slug,
            sellerOneProduct.ShortDescription,
            sellerOneProduct.FullDescription,
            "[]",
            "{}");
        listingRevision.SubmitForReview(true, now);
        var variantRevision = new ProductVariantRevision(sellerOneProduct.Id, sellerOne.SellerId);
        variantRevision.UpdateSellerReason("Seasonal price update.");
        variantRevision.SubmitForReview(true, now);

        var returnRequest = new ReturnRequest(
            paidOrder.Id,
            buyerId,
            sellerOne.SellerId,
            ReturnReason.DamagedItem,
            "Damaged on arrival.",
            now);
        returnRequest.MarkAwaitingSellerResponse(now);
        var supportTicket = new SupportTicket(
            sellerOne.UserId,
            "Seller",
            buyerId: null,
            sellerId: sellerOne.SellerId,
            SupportTicketCategory.Other,
            "Inventory question",
            "Please help with inventory.",
            linkedOrderId: null,
            linkedProductId: sellerOneProduct.Id,
            linkedSellerId: sellerOne.SellerId,
            linkedPaymentId: null,
            now);
        var dispute = new Dispute(paidOrder.Id, null, buyerId, sellerOne.SellerId, buyerId, "Item issue.", now);

        var balance = new SellerBalance(sellerOne.SellerId, "ZAR");
        balance.CreditPending(3500m);
        balance.HoldPending(100m);
        balance.CreditAvailable(1200m);
        var payout = new SellerPayout(sellerOne.SellerId, 300m, "ZAR", now);
        var payoutProfileChange = new SellerPayoutProfileChangeRequest(
            sellerOne.SellerId,
            "new-provider-reference",
            "Re-verify payout details.",
            sellerOne.UserId);
        payoutProfileChange.Submit(now);

        var activeCampaign = new AdCampaign(
            sellerOne.SellerId,
            "Active dashboard campaign",
            AdCampaignType.FeaturedProduct,
            now.AddDays(-1),
            now.AddDays(10),
            now.AddDays(-1));
        activeCampaign.ReplaceProducts([sellerOneProduct.Id], now.AddDays(-1));
        activeCampaign.SubmitForReview(now.AddDays(-1));
        activeCampaign.Approve(Guid.NewGuid(), now.AddDays(-1));
        var pendingCampaign = new AdCampaign(
            sellerOne.SellerId,
            "Pending dashboard campaign",
            AdCampaignType.FeaturedProduct,
            now,
            now.AddDays(10),
            now);
        pendingCampaign.ReplaceProducts([sellerOneProduct.Id], now);
        pendingCampaign.SubmitForReview(now);
        var click = new AdClick(activeCampaign.Id, sellerOneProduct.Id, buyerId, null, now);

        dbContext.Products.AddRange(sellerOneProduct, sellerTwoProduct, pendingProduct, changesProduct);
        dbContext.ProductVariants.AddRange(sellerOneVariant, reservedVariant, outOfStockVariant, otherSellerVariant);
        dbContext.Orders.AddRange(paidOrder, processingOrder, readyOrder, exceptionOrder, otherSellerOrder);
        dbContext.Shipments.Add(exceptionShipment);
        dbContext.ProductListingRevisions.Add(listingRevision);
        dbContext.ProductVariantRevisions.Add(variantRevision);
        dbContext.ReturnRequests.Add(returnRequest);
        dbContext.SupportTickets.Add(supportTicket);
        dbContext.Disputes.Add(dispute);
        dbContext.SellerBalances.Add(balance);
        dbContext.SellerPayouts.Add(payout);
        dbContext.SellerPayoutProfileChangeRequests.Add(payoutProfileChange);
        dbContext.AdCampaigns.AddRange(activeCampaign, pendingCampaign);
        dbContext.AdClicks.Add(click);
        dbContext.AdCharges.Add(new AdCharge(activeCampaign.Id, click.Id, 25m, "ZAR", "Click", now));
        dbContext.AdConversions.Add(new AdConversion(activeCampaign.Id, click.Id, paidOrder.Id, paidOrder.TotalAmount, "ZAR", now));
        dbContext.Notifications.Add(new Notification(
            sellerOne.UserId,
            "SellerProductApproved",
            "Product approved",
            "Your product was approved.",
            "Product",
            sellerOneProduct.Id,
            now));

        await dbContext.SaveChangesAsync();
    }

    private static Product CreatePublishedProduct(Guid sellerId, string title)
    {
        var product = CreateReviewProduct(sellerId, title);
        product.Publish(DateTimeOffset.UtcNow.AddDays(-3));
        return product;
    }

    private static Product CreateReviewProduct(Guid sellerId, string title)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            title,
            $"{title.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            "Short description.",
            "Full product description.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        return product;
    }

    private sealed record SellerAuthContext(Guid SellerId, Guid UserId, string AccessToken);

    private sealed class SellerDashboardTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleSellerDashboardTests_{Guid.NewGuid():N}";

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
