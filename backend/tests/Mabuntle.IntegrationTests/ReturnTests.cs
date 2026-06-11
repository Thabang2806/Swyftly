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
using Mabuntle.Api.Returns;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Returns;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class ReturnTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanRequestReturnForDeliveredOrder_AndSellerPayoutIsHeld()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-return@example.test", MabuntleRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-return@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);
        await SeedPayoutAsync(factory, sellerId, order.OrderId, order.OrderItemId, 875m);

        using var response = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "DamagedItem",
                "The item arrived damaged.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "DamagedItem", false, "Torn seam.")
                ]));

        response.EnsureSuccessStatusCode();
        var returnRequest = await response.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);
        Assert.Equal("AwaitingSellerResponse", returnRequest!.Status);
        Assert.Single(returnRequest.Items);
        Assert.NotNull(returnRequest.SellerPolicySnapshot);
        Assert.Equal(14, returnRequest.SellerPolicySnapshot!.ReturnWindowDays);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
            var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == sellerId);
            Assert.Equal(0m, balance.PendingBalance);
            Assert.Equal(875m, balance.HeldBalance);
            Assert.Equal(SellerPayoutStatus.OnHold, payout.Status);
            Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "PayoutHeld"));
            var movement = await dbContext.InventoryMovements.SingleAsync(movement => movement.ReturnRequestId == returnRequest.ReturnRequestId);
            Assert.Equal(InventoryMovementType.ReturnRequested, movement.MovementType);
            Assert.Equal(order.OrderId, movement.OrderId);
            Assert.Equal(0, movement.QuantityDelta);
            Assert.Equal(0, movement.ReservedQuantityAfter - movement.ReservedQuantityBefore);
        }

        using var sellerResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest.ReturnRequestId}/approve",
            new SellerReturnResponseApiRequest("Return approved."));

        sellerResponse.EnsureSuccessStatusCode();
        var approved = await sellerResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(approved);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("Returns are reviewed for delivered items in original condition.", approved.SellerPolicySnapshot!.ReturnPolicy);
        Assert.Contains(approved.Messages, message => message.SenderRole == "Seller");

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReturnApproved" && notification.RelatedEntityId == returnRequest.ReturnRequestId);
    }

    [Fact]
    public async Task BuyerCannotRequestReturnForUndeliveredOrder()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        var sellerUserId = await RegisterSellerAsync(factory, "seller-undelivered-return@example.test");
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-undelivered-return@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedOrderAsync(factory, buyerId, sellerId, OrderStatus.Shipped);

        using var response = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "WrongSize",
                "Size issue.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "WrongSize", false, null)
                ]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SellerCanRecordRestockDecisionForApprovedReturn()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-restock-return@example.test", MabuntleRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-restock-return@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "WrongSize",
                "The fit did not work.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "WrongSize", false, null)
                ]));
        createResponse.EnsureSuccessStatusCode();
        var returnRequest = await createResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);

        using var approveResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest!.ReturnRequestId}/approve",
            new SellerReturnResponseApiRequest("Approved after checking the order."));
        approveResponse.EnsureSuccessStatusCode();

        using var restockResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest.ReturnRequestId}/restock-decisions",
            new SellerReturnRestockDecisionApiRequest(
            [
                new SellerReturnRestockDecisionItemApiRequest(
                    returnRequest.Items.Single().ReturnItemId,
                    1,
                    "Sellable",
                    "Inspected and returned to sellable stock.")
            ]));

        restockResponse.EnsureSuccessStatusCode();
        var decisions = await restockResponse.Content.ReadFromJsonAsync<SellerReturnRestockDecisionResponse[]>();
        Assert.NotNull(decisions);
        var decision = Assert.Single(decisions!);
        Assert.Equal(1, decision.QuantityRestocked);
        Assert.Equal("Sellable", decision.Condition);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var variant = await dbContext.ProductVariants.SingleAsync(variant => variant.Id == order.ProductVariantId);
            Assert.Equal(6, variant.StockQuantity);
            Assert.Equal(1, await dbContext.ReturnRestockDecisions.CountAsync(item => item.ReturnRequestId == returnRequest.ReturnRequestId));
            var movement = await dbContext.InventoryMovements.SingleAsync(
                movement => movement.ReturnRequestId == returnRequest.ReturnRequestId
                    && movement.MovementType == InventoryMovementType.ReturnRestocked);
            Assert.Equal(order.OrderId, movement.OrderId);
            Assert.Equal(1, movement.QuantityDelta);
            Assert.Equal(0, movement.ReservedQuantityAfter - movement.ReservedQuantityBefore);
            Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "SellerReturnRestockDecisionRecorded"));
        }
    }

    [Fact]
    public async Task SellerRestockDecisionIsOnePerReturnItem()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-restock-duplicate@example.test", MabuntleRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-restock-duplicate@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "WrongSize",
                "The fit did not work.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "WrongSize", false, null)
                ]));
        createResponse.EnsureSuccessStatusCode();
        var returnRequest = await createResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);

        using var approveResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest!.ReturnRequestId}/approve",
            new SellerReturnResponseApiRequest("Approved after checking the order."));
        approveResponse.EnsureSuccessStatusCode();

        var request = new SellerReturnRestockDecisionApiRequest(
        [
            new SellerReturnRestockDecisionItemApiRequest(
                returnRequest.Items.Single().ReturnItemId,
                0,
                "Damaged",
                "Item is not sellable.")
        ]);
        using var firstResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest.ReturnRequestId}/restock-decisions",
            request);
        firstResponse.EnsureSuccessStatusCode();

        using var duplicateResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest.ReturnRequestId}/restock-decisions",
            request);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var variant = await dbContext.ProductVariants.SingleAsync(variant => variant.Id == order.ProductVariantId);
        Assert.Equal(5, variant.StockQuantity);
        Assert.Equal(1, await dbContext.ReturnRestockDecisions.CountAsync(item => item.ReturnRequestId == returnRequest.ReturnRequestId));
        Assert.False(await dbContext.InventoryMovements.AnyAsync(
            movement => movement.ReturnRequestId == returnRequest.ReturnRequestId
                && movement.MovementType == InventoryMovementType.ReturnRestocked));
    }

    [Fact]
    public async Task AdminCanViewDisputedReturns()
    {
        await using var factory = new ReturnTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-disputed-return@example.test", MabuntleRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-disputed-return@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var order = await SeedDeliveredOrderAsync(factory, buyerId, sellerId);

        using var createResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{order.OrderId}/returns",
            new CreateReturnRequestApiRequest(
                "NotAsDescribed",
                "Listing did not match.",
                [
                    new CreateReturnItemApiRequest(order.OrderItemId, 1, "NotAsDescribed", false, null)
                ]));
        createResponse.EnsureSuccessStatusCode();
        var returnRequest = await createResponse.Content.ReadFromJsonAsync<ReturnRequestResult>();
        Assert.NotNull(returnRequest);

        using var rejectResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/returns/{returnRequest!.ReturnRequestId}/reject",
            new SellerReturnResponseApiRequest("Seller rejects the claim."));
        rejectResponse.EnsureSuccessStatusCode();

        using var notificationsResponse = await buyerClient.GetAsync("/api/buyer/notifications");
        notificationsResponse.EnsureSuccessStatusCode();
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<NotificationResult[]>();
        Assert.Contains(notifications!, notification => notification.Type == "ReturnRejected" && notification.RelatedEntityId == returnRequest.ReturnRequestId);

        using var disputeResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/returns/{returnRequest.ReturnRequestId}/dispute",
            new DisputeReturnRequestApiRequest("Please review the listing photos."));
        disputeResponse.EnsureSuccessStatusCode();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginAdminAsync(factory, adminClient));
        using var adminResponse = await adminClient.GetAsync("/api/admin/returns/disputed");

        adminResponse.EnsureSuccessStatusCode();
        var disputedReturns = await adminResponse.Content.ReadFromJsonAsync<ReturnRequestResult[]>();
        Assert.NotNull(disputedReturns);
        var disputed = Assert.Single(disputedReturns!);
        Assert.Equal("Disputed", disputed.Status);
        Assert.Equal(returnRequest.ReturnRequestId, disputed.ReturnRequestId);
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private static async Task<Guid> RegisterSellerAsync(ReturnTestFactory factory, string email)
    {
        using var client = factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, email, MabuntleRoles.Seller);
        return auth.UserId;
    }

    private static async Task<Guid> GetSellerIdAsync(ReturnTestFactory factory, Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == sellerUserId)
            .Select(seller => seller.Id)
            .SingleAsync();
    }

    private static async Task<Guid> GetBuyerIdAsync(ReturnTestFactory factory, Guid buyerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.BuyerProfiles
            .Where(buyer => buyer.UserId == buyerUserId)
            .Select(buyer => buyer.Id)
            .SingleAsync();
    }

    private static Task<SeededOrder> SeedDeliveredOrderAsync(ReturnTestFactory factory, Guid buyerId, Guid sellerId) =>
        SeedOrderAsync(factory, buyerId, sellerId, OrderStatus.Delivered);

    private static async Task<SeededOrder> SeedOrderAsync(
        ReturnTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        OrderStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var product = new Product(sellerId);
        var variant = new ProductVariant(product.Id, "SKU-RETURN", "M", "Black", 1000m, 1200m, 5);
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.SetSellerPolicySnapshot(
            new SellerStorePolicy(
                sellerId,
                14,
                "Returns are reviewed for delivered items in original condition.",
                "Exchanges depend on stock availability.",
                "Orders are usually dispatched within 2-3 business days.",
                "Message support with order issues and product questions.",
                "Follow product care notes on each item.",
                "Colour and fit may vary slightly by screen and size."),
            now);
        order.AddItem(product.Id, variant.Id, "Returned Item", variant.Sku, variant.Size, variant.Colour, variant.Price, 1);
        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        }

        if (status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(2), "TestShipped");
        }

        if (status == OrderStatus.Delivered)
        {
            order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(3), "TestDelivered");
        }

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return new SeededOrder(order.Id, order.Items.Single().Id, variant.Id);
    }

    private static async Task SeedPayoutAsync(
        ReturnTestFactory factory,
        Guid sellerId,
        Guid orderId,
        Guid orderItemId,
        decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:05:00Z");
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(amount);
        var ledgerEntry = new LedgerEntry(
            orderId,
            orderItemId,
            sellerId,
            null,
            null,
            LedgerEntryType.SellerPendingBalanceCredited,
            amount,
            "ZAR",
            LedgerDirection.Credit,
            "Seller pending balance credited.",
            createdAt);
        var payout = new SellerPayout(sellerId, amount, "ZAR", createdAt);
        payout.AddItem(ledgerEntry.Id, orderId, null, amount, createdAt);

        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> CreateAndLoginAdminAsync(ReturnTestFactory factory, HttpClient client)
    {
        var email = $"admin-return-{Guid.NewGuid():N}@example.test";
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
            var createResult = await userManager.CreateAsync(admin, TestPassword);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(admin, MabuntleRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed record SeededOrder(Guid OrderId, Guid OrderItemId, Guid ProductVariantId);

    private sealed class ReturnTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleReturnTests_{Guid.NewGuid():N}";

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
