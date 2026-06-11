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
using Mabuntle.Api.Refunds;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Refunds;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class RefundTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanReadOwnRefunds_ByListDetailOrderAndReturn()
    {
        await using var factory = new RefundTestFactory();
        using var buyerClient = factory.CreateClient();
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-refund-reader@example.test", MabuntleRoles.Buyer);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var seed = await SeedBuyerVisibleRefundAsync(factory, buyerId);

        using var listResponse = await buyerClient.GetAsync("/api/buyer/refunds");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<BuyerRefundResult[]>();
        Assert.NotNull(list);
        var listedRefund = Assert.Single(list!);
        Assert.Equal(seed.RefundId, listedRefund.RefundId);
        Assert.Equal("Processing", listedRefund.Status);
        Assert.Contains("provider action", listedRefund.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manual provider payload", string.Join(" ", listedRefund.Timeline.Select(item => item.Message)), StringComparison.OrdinalIgnoreCase);

        using var detailResponse = await buyerClient.GetAsync($"/api/buyer/refunds/{seed.RefundId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<BuyerRefundResult>();
        Assert.NotNull(detail);
        Assert.Equal(seed.OrderId, detail!.OrderId);
        Assert.Equal(seed.ReturnRequestId, detail.ReturnRequestId);
        Assert.Equal(500m, detail.Amount);

        using var orderResponse = await buyerClient.GetAsync($"/api/buyer/orders/{seed.OrderId}/refunds");
        orderResponse.EnsureSuccessStatusCode();
        var orderRefunds = await orderResponse.Content.ReadFromJsonAsync<BuyerRefundResult[]>();
        Assert.NotNull(orderRefunds);
        Assert.Single(orderRefunds!);

        using var returnResponse = await buyerClient.GetAsync($"/api/buyer/returns/{seed.ReturnRequestId}/refunds");
        returnResponse.EnsureSuccessStatusCode();
        var returnRefunds = await returnResponse.Content.ReadFromJsonAsync<BuyerRefundResult[]>();
        Assert.NotNull(returnRefunds);
        Assert.Single(returnRefunds!);
    }

    [Fact]
    public async Task BuyerRefundReads_AreScopedToAuthenticatedBuyer()
    {
        await using var factory = new RefundTestFactory();
        using var firstBuyerClient = factory.CreateClient();
        using var secondBuyerClient = factory.CreateClient();
        var firstBuyerAuth = await RegisterAndLoginAsync(firstBuyerClient, "buyer-refund-owner@example.test", MabuntleRoles.Buyer);
        await RegisterAndLoginAsync(secondBuyerClient, "buyer-refund-other@example.test", MabuntleRoles.Buyer);
        var firstBuyerId = await GetBuyerIdAsync(factory, firstBuyerAuth.UserId);
        var seed = await SeedBuyerVisibleRefundAsync(factory, firstBuyerId);

        using var listResponse = await secondBuyerClient.GetAsync("/api/buyer/refunds");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<BuyerRefundResult[]>();
        Assert.NotNull(list);
        Assert.Empty(list!);

        using var detailResponse = await secondBuyerClient.GetAsync($"/api/buyer/refunds/{seed.RefundId}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, detailResponse.StatusCode);

        using var orderResponse = await secondBuyerClient.GetAsync($"/api/buyer/orders/{seed.OrderId}/refunds");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, orderResponse.StatusCode);

        using var returnResponse = await secondBuyerClient.GetAsync($"/api/buyer/returns/{seed.ReturnRequestId}/refunds");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, returnResponse.StatusCode);
    }

    [Fact]
    public async Task AdminCanApproveFullRefund_AndLedgerReversalsAdjustSellerBalance()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceOperator));

        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(1000m, "Approved full refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);
        Assert.Equal("Requested", requested!.Status);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceApprover));
        using var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested.RefundId}/approve",
            new ApproveRefundApiRequest("Return approved by admin."));
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(approved);
        Assert.Equal("Refunded", approved!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var payment = await dbContext.Payments.SingleAsync(payment => payment.Id == seed.PaymentId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == seed.OrderId);
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == seed.SellerId);
        var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == seed.SellerId);
        var refundReversals = await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundReversal)
            .ToListAsync();
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(0m, balance.PendingBalance);
        Assert.Equal(0m, payout.Amount);
        Assert.Equal(SellerPayoutStatus.Reversed, payout.Status);
        Assert.Equal(1, await dbContext.SellerPayoutAdjustments.CountAsync(adjustment => adjustment.RefundId == approved.RefundId));
        Assert.Contains(refundReversals, entry => entry.Amount == 875m && entry.Direction == LedgerDirection.Debit);
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "RefundApproved"));
    }

    [Fact]
    public async Task AdminCanApprovePartialRefund_AndSellerBalanceUsesProportionalDebit()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceOperator));

        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(500m, "Approved partial refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceApprover));
        using var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested!.RefundId}/approve",
            new ApproveRefundApiRequest("Partial refund approved by admin."));
        approveResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var payment = await dbContext.Payments.SingleAsync(payment => payment.Id == seed.PaymentId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == seed.OrderId);
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == seed.SellerId);
        var payout = await dbContext.SellerPayouts.Include(payout => payout.Items).SingleAsync(payout => payout.SellerId == seed.SellerId);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(437.50m, balance.PendingBalance);
        Assert.Equal(437.50m, payout.Amount);
        Assert.Equal(437.50m, payout.Items.Single().AdjustedAmount);
        Assert.Equal(437.50m, await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == seed.PaymentId
                && entry.Type == LedgerEntryType.RefundReversal
                && entry.Description == "Seller balance refund reversal.")
            .Select(entry => entry.Amount)
            .SingleAsync());
    }

    [Fact]
    public async Task AdminDuplicateRefundApproval_ReturnsExistingRefundWithoutDuplicateLedgerOrAudit()
    {
        await using var factory = new RefundTestFactory();
        using var adminClient = factory.CreateClient();
        var seed = await SeedPaidOrderAsync(factory, amount: 1000m);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceOperator));
        using var createResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/orders/{seed.OrderId}/refunds",
            new CreateRefundApiRequest(500m, "Approved duplicate-check refund."));
        createResponse.EnsureSuccessStatusCode();
        var requested = await createResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(requested);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, adminClient, MabuntleRoles.FinanceApprover));
        using var firstApproveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested!.RefundId}/approve",
            new ApproveRefundApiRequest("First approval."));
        using var secondApproveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{requested.RefundId}/approve",
            new ApproveRefundApiRequest("Duplicate approval."));

        firstApproveResponse.EnsureSuccessStatusCode();
        secondApproveResponse.EnsureSuccessStatusCode();
        var first = await firstApproveResponse.Content.ReadFromJsonAsync<RefundResult>();
        var second = await secondApproveResponse.Content.ReadFromJsonAsync<RefundResult>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.ProviderRefundReference, second!.ProviderRefundReference);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundIssued));
        Assert.Equal(3, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == seed.PaymentId && entry.Type == LedgerEntryType.RefundReversal));
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "RefundApproved" && auditLog.EntityId == requested.RefundId.ToString()));
    }

    private static async Task<SeededRefundOrder> SeedPaidOrderAsync(RefundTestFactory factory, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Refundable Item", "SKU-REFUND", "M", "Black", amount, 1);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(2), "TestShipped");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(3), "TestDelivered");
        var payment = new Payment(order.Id, buyerId, "Fake", amount, "ZAR", now);
        payment.SetProviderReference($"fake_{order.Id:N}", now);
        payment.MarkPaid(now);
        var sellerPending = 875m;
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(sellerPending);
        var buyerPaymentEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.BuyerPaymentReceived, 1000m, "ZAR", LedgerDirection.Credit, "Buyer payment received.", now);
        var platformCommissionEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PlatformCommissionRecorded, 100m, "ZAR", LedgerDirection.Credit, "Platform commission recorded.", now);
        var providerFeeEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PaymentProviderFeeRecorded, 25m, "ZAR", LedgerDirection.Debit, "Payment provider fee recorded.", now);
        var sellerPendingEntry = new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.SellerPendingBalanceCredited, sellerPending, "ZAR", LedgerDirection.Credit, "Seller pending balance credited.", now);
        var payout = new SellerPayout(sellerId, sellerPending, "ZAR", now);
        payout.AddItem(sellerPendingEntry.Id, order.Id, payment.Id, sellerPending, now);

        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.AddRange(
            buyerPaymentEntry,
            platformCommissionEntry,
            providerFeeEntry,
            sellerPendingEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return new SeededRefundOrder(order.Id, payment.Id, sellerId);
    }

    private static async Task<SeededBuyerRefund> SeedBuyerVisibleRefundAsync(RefundTestFactory factory, Guid buyerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var sellerId = Guid.NewGuid();
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Refund visible item", "SKU-BUYER-REFUND", "M", "Black", 1000m, 1);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(2), "TestDelivered");
        var orderItem = order.Items.Single();
        var payment = new Payment(order.Id, buyerId, "Fake", 1000m, "ZAR", now);
        payment.SetProviderReference($"fake_{order.Id:N}", now);
        payment.MarkPaid(now.AddMinutes(1));
        var returnRequest = new ReturnRequest(order.Id, buyerId, sellerId, ReturnReason.DamagedItem, "Item arrived damaged.", now.AddMinutes(3));
        returnRequest.AddItem(orderItem.Id, orderItem.ProductId, orderItem.ProductVariantId, 1, ReturnReason.DamagedItem, false, "Packaging damaged.");
        returnRequest.MarkAwaitingSellerResponse(now.AddMinutes(4));
        returnRequest.Approve(Guid.NewGuid(), "Seller approved return.", now.AddMinutes(5));
        var refund = new Refund(
            order.Id,
            payment.Id,
            buyerId,
            sellerId,
            returnRequest.Id,
            500m,
            "ZAR",
            "Internal finance reason with manual provider payload detail.",
            Guid.NewGuid(),
            MabuntleRoles.FinanceOperator,
            now.AddMinutes(6));
        refund.Approve(Guid.NewGuid(), "Finance approval reason not shown to buyer.", now.AddMinutes(7));
        refund.MarkProcessing(now.AddMinutes(8));
        refund.MarkProviderActionRequired("Manual provider payload says action is required.", now.AddMinutes(9));

        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.ReturnRequests.Add(returnRequest);
        dbContext.Refunds.Add(refund);
        await dbContext.SaveChangesAsync();

        return new SeededBuyerRefund(order.Id, returnRequest.Id, refund.Id);
    }

    private static async Task<string> CreateAndLoginFinanceUserAsync(RefundTestFactory factory, HttpClient client, string role)
    {
        var email = $"finance-refund-{Guid.NewGuid():N}@example.test";
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
            var roleResult = await userManager.AddToRoleAsync(admin, role);
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

    private static async Task<Guid> GetBuyerIdAsync(RefundTestFactory factory, Guid buyerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.BuyerProfiles
            .Where(buyer => buyer.UserId == buyerUserId)
            .Select(buyer => buyer.Id)
            .SingleAsync();
    }

    private sealed record SeededRefundOrder(Guid OrderId, Guid PaymentId, Guid SellerId);

    private sealed record SeededBuyerRefund(Guid OrderId, Guid ReturnRequestId, Guid RefundId);

    private sealed class RefundTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleRefundTests_{Guid.NewGuid():N}";

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
