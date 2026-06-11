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
using Mabuntle.Api.Disputes;
using Mabuntle.Application.Disputes;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class DisputeTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task BuyerCanOpenDispute_SellerCanRespond_AdminCanResolveSellerFavouredAndReleasePayout()
    {
        await using var factory = new DisputeTestFactory();
        using var buyerClient = factory.CreateClient();
        using var sellerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        var sellerAuth = await RegisterAndLoginAsync(sellerClient, "seller-dispute@example.test", MabuntleRoles.Seller);
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-dispute@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerAuth.UserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var orderId = await SeedDeliveredOrderWithPayoutAsync(factory, buyerId, sellerId, 875m);

        using var openResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderId}/disputes",
            new OpenDisputeApiRequest(
                "Item appears counterfeit.",
                [
                    new DisputeEvidenceApiRequest("Image", "uploads/disputes/photo.jpg", "Logo mismatch.")
                ]));
        openResponse.EnsureSuccessStatusCode();
        var opened = await openResponse.Content.ReadFromJsonAsync<DisputeResult>();
        Assert.NotNull(opened);
        Assert.Equal("AwaitingSeller", opened!.Status);
        Assert.Single(opened.Evidence);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
            var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == sellerId);
            var order = await dbContext.Orders.SingleAsync(order => order.Id == orderId);
            Assert.Equal(0m, balance.PendingBalance);
            Assert.Equal(875m, balance.HeldBalance);
            Assert.Equal(SellerPayoutStatus.OnHold, payout.Status);
            Assert.Equal(OrderStatus.Disputed, order.Status);
        }

        using var sellerMessageResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/disputes/{opened.DisputeId}/messages",
            new DisputeMessageApiRequest("Supplier certificate attached."));
        sellerMessageResponse.EnsureSuccessStatusCode();

        using var sellerEvidenceResponse = await sellerClient.PostAsJsonAsync(
            $"/api/seller/disputes/{opened.DisputeId}/evidence",
            new DisputeEvidenceApiRequest("Document", "uploads/disputes/certificate.pdf", "Supplier certificate."));
        sellerEvidenceResponse.EnsureSuccessStatusCode();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginAdminAsync(factory, adminClient));
        using var resolveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/disputes/{opened.DisputeId}/resolve",
            new ResolveDisputeApiRequest("SellerFavoured", "Seller evidence accepted."));
        resolveResponse.EnsureSuccessStatusCode();
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<DisputeResult>();
        Assert.NotNull(resolved);
        Assert.Equal("ResolvedSellerFavoured", resolved!.Status);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
            var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == sellerId);
            Assert.Equal(875m, balance.PendingBalance);
            Assert.Equal(0m, balance.HeldBalance);
            Assert.Equal(SellerPayoutStatus.Pending, payout.Status);
            Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "DisputeResolved"));
        }
    }

    [Fact]
    public async Task AdminCanResolveBuyerFavoured_CreatesRefundRequestAndKeepsPayoutHeld()
    {
        await using var factory = new DisputeTestFactory();
        using var buyerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        var sellerUserId = await RegisterSellerAsync(factory, "seller-buyer-favoured-dispute@example.test");
        var buyerAuth = await RegisterAndLoginAsync(buyerClient, "buyer-favoured-dispute@example.test", MabuntleRoles.Buyer);
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var buyerId = await GetBuyerIdAsync(factory, buyerAuth.UserId);
        var orderId = await SeedDeliveredOrderWithPayoutAsync(factory, buyerId, sellerId, 875m);

        using var openResponse = await buyerClient.PostAsJsonAsync(
            $"/api/buyer/orders/{orderId}/disputes",
            new OpenDisputeApiRequest("Wrong item received.", []));
        openResponse.EnsureSuccessStatusCode();
        var opened = await openResponse.Content.ReadFromJsonAsync<DisputeResult>();
        Assert.NotNull(opened);

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginAdminAsync(factory, adminClient));
        using var resolveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/disputes/{opened!.DisputeId}/resolve",
            new ResolveDisputeApiRequest("BuyerFavoured", "Buyer evidence accepted."));
        resolveResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var payout = await dbContext.SellerPayouts.SingleAsync(payout => payout.SellerId == sellerId);
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
        var refund = await dbContext.Refunds.SingleAsync(refund => refund.OrderId == orderId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == orderId);
        Assert.Equal(SellerPayoutStatus.OnHold, payout.Status);
        Assert.Equal(875m, balance.HeldBalance);
        Assert.Equal(RefundStatus.Requested, refund.Status);
        Assert.Equal(1000m, refund.Amount);
        Assert.Equal(order.BuyerId, refund.BuyerId);
        Assert.Equal(order.SellerId, refund.SellerId);
        Assert.Contains(opened.DisputeId.ToString(), refund.Reason, StringComparison.Ordinal);
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

    private static async Task<Guid> RegisterSellerAsync(DisputeTestFactory factory, string email)
    {
        using var client = factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, email, MabuntleRoles.Seller);
        return auth.UserId;
    }

    private static async Task<Guid> GetSellerIdAsync(DisputeTestFactory factory, Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.SellerProfiles.Where(seller => seller.UserId == sellerUserId).Select(seller => seller.Id).SingleAsync();
    }

    private static async Task<Guid> GetBuyerIdAsync(DisputeTestFactory factory, Guid buyerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.BuyerProfiles.Where(buyer => buyer.UserId == buyerUserId).Select(buyer => buyer.Id).SingleAsync();
    }

    private static async Task<Guid> SeedDeliveredOrderWithPayoutAsync(
        DisputeTestFactory factory,
        Guid buyerId,
        Guid sellerId,
        decimal payoutAmount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Disputed Item", "SKU-DISPUTE", "M", "Black", 1000m, 1);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        order.ChangeStatus(OrderStatus.Shipped, now.AddMinutes(2), "TestShipped");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(3), "TestDelivered");
        var payment = new Payment(order.Id, buyerId, "Fake", 1000m, "ZAR", now);
        payment.SetProviderReference($"fake_{order.Id:N}", now);
        payment.MarkPaid(now.AddMinutes(1));
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(payoutAmount);
        var ledgerEntry = new LedgerEntry(
            order.Id,
            order.Items.Single().Id,
            sellerId,
            buyerId,
            payment.Id,
            LedgerEntryType.SellerPendingBalanceCredited,
            payoutAmount,
            "ZAR",
            LedgerDirection.Credit,
            "Seller pending balance credited.",
            now);
        var payout = new SellerPayout(sellerId, payoutAmount, "ZAR", now);
        payout.AddItem(ledgerEntry.Id, order.Id, payment.Id, payoutAmount, now);

        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private static async Task<string> CreateAndLoginAdminAsync(DisputeTestFactory factory, HttpClient client)
    {
        var email = $"admin-dispute-{Guid.NewGuid():N}@example.test";
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

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class DisputeTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleDisputeTests_{Guid.NewGuid():N}";

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
