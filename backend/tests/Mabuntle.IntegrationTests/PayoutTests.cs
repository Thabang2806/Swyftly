using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Payouts;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Ledger;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class PayoutTests
{
    [Fact]
    public async Task Seller_CanViewBalanceAndPayouts()
    {
        await using var factory = new PayoutTestFactory();
        using var client = factory.CreateClient();
        var sellerUserId = await CreateSellerUserAsync(factory, client, "seller-payout@example.test");
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        await SeedPayoutAsync(factory, sellerId, 875m);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, "seller-payout@example.test"));

        using var balanceResponse = await client.GetAsync("/api/seller/balance");
        using var payoutsResponse = await client.GetAsync("/api/seller/payouts");

        balanceResponse.EnsureSuccessStatusCode();
        payoutsResponse.EnsureSuccessStatusCode();
        var balance = await balanceResponse.Content.ReadFromJsonAsync<SellerBalanceResponse>();
        var payoutsJson = await payoutsResponse.Content.ReadAsStringAsync();
        var payouts = JsonSerializer.Deserialize<SellerPayoutResponse[]>(
            payoutsJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(balance);
        Assert.Equal(875m, Assert.Single(balance!.Balances).PendingBalance);
        Assert.NotNull(payouts);
        var payout = Assert.Single(payouts!);
        Assert.Equal("Pending", payout.Status);
        Assert.Equal("Ledger", Assert.Single(payout.Items).SourceType);
        Assert.DoesNotContain("ledgerEntryId", payoutsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orderId", payoutsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paymentId", payoutsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_CanHoldAndReleasePayout_AndAuditLogsAreWritten()
    {
        await using var factory = new PayoutTestFactory();
        using var client = factory.CreateClient();
        var sellerUserId = await CreateSellerUserAsync(factory, client, "seller-admin-payout@example.test");
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var payoutId = await SeedPayoutAsync(factory, sellerId, 875m);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, MabuntleRoles.FinanceOperator));

        using var holdResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/hold",
            new PayoutReasonRequest("Dispute review."));
        holdResponse.EnsureSuccessStatusCode();
        var held = await holdResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(held);
        Assert.Equal("OnHold", held!.Status);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await CreateAndLoginFinanceUserAsync(factory, client, MabuntleRoles.FinanceApprover));
        using var releaseResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/release",
            new PayoutReasonRequest("Review complete."));
        releaseResponse.EnsureSuccessStatusCode();
        var released = await releaseResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(released);
        Assert.Equal("Pending", released!.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
        Assert.Equal(875m, balance.PendingBalance);
        Assert.Equal(0m, balance.HeldBalance);
        Assert.Equal(2, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.EntityType == "SellerPayout"));
    }

    [Fact]
    public async Task FinanceLifecycle_MakeAvailableAndProcess_UpdatesBalancesAndEnforcesDualControl()
    {
        await using var factory = new PayoutTestFactory();
        using var client = factory.CreateClient();
        var sellerUserId = await CreateSellerUserAsync(factory, client, "seller-lifecycle-payout@example.test");
        var sellerId = await GetSellerIdAsync(factory, sellerUserId);
        var payoutId = await SeedPayoutAsync(factory, sellerId, 875m);
        var operatorToken = await CreateAndLoginFinanceUserAsync(factory, client, MabuntleRoles.FinanceOperator);
        var approverToken = await CreateAndLoginFinanceUserAsync(factory, client, MabuntleRoles.FinanceApprover);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        using var makeAvailableResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/make-available",
            new PayoutReasonRequest("Settlement window reached."));
        makeAvailableResponse.EnsureSuccessStatusCode();
        var available = await makeAvailableResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(available);
        Assert.Equal("Available", available!.Status);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        using var blockedProcessResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/process",
            new PayoutReasonRequest("Attempted by same operator."));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, blockedProcessResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approverToken);
        using var processResponse = await client.PostAsJsonAsync(
            $"/api/admin/payouts/{payoutId}/process",
            new PayoutReasonRequest("Approved for payout."));
        processResponse.EnsureSuccessStatusCode();
        var processed = await processResponse.Content.ReadFromJsonAsync<SellerPayoutResult>();
        Assert.NotNull(processed);
        Assert.Equal("PaidOut", processed!.Status);
        Assert.False(string.IsNullOrWhiteSpace(processed.ProviderPayoutReference));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var balance = await dbContext.SellerBalances.SingleAsync(balance => balance.SellerId == sellerId);
        Assert.Equal(0m, balance.PendingBalance);
        Assert.Equal(0m, balance.AvailableBalance);
        Assert.Equal(0m, balance.HeldBalance);
    }

    private static async Task<Guid> CreateSellerUserAsync(PayoutTestFactory factory, HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", MabuntleRoles.Seller));
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId != Guid.Empty)
            .OrderByDescending(seller => seller.CreatedAtUtc)
            .Select(seller => seller.UserId)
            .FirstAsync();
    }

    private static async Task<Guid> GetSellerIdAsync(PayoutTestFactory factory, Guid sellerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == sellerUserId)
            .Select(seller => seller.Id)
            .SingleAsync();
    }

    private static async Task<Guid> SeedPayoutAsync(PayoutTestFactory factory, Guid sellerId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(amount);
        var ledgerEntry = new LedgerEntry(
            null,
            null,
            sellerId,
            null,
            null,
            LedgerEntryType.SellerPendingBalanceCredited,
            amount,
            "ZAR",
            LedgerDirection.Credit,
            "Seller pending balance credited.",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        var payout = new SellerPayout(sellerId, amount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payout.AddItem(ledgerEntry.Id, null, null, amount, DateTimeOffset.Parse("2026-05-18T12:00:00Z"));

        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.SellerPayouts.Add(payout);
        await dbContext.SaveChangesAsync();
        return payout.Id;
    }

    private static async Task<string> CreateAndLoginFinanceUserAsync(PayoutTestFactory factory, HttpClient client, string role)
    {
        var email = $"finance-payout-{Guid.NewGuid():N}@example.test";
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
            var roleResult = await userManager.AddToRoleAsync(admin, role);
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

    private sealed class PayoutTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntlePayoutTests_{Guid.NewGuid():N}";

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
