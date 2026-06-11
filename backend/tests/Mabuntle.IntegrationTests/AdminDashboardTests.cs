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
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminDashboardTests
{
    [Fact]
    public async Task Buyer_CannotAccessAdminDashboard()
    {
        using var factory = new AdminDashboardTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadDashboardSummaryCounts()
    {
        using var factory = new AdminDashboardTestFactory();
        using var client = factory.CreateClient();
        await SeedDashboardDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/dashboard/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<AdminDashboardSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.PendingSellerApprovals);
        Assert.Equal(1, summary.PendingProductReviews);
        Assert.Equal(1, summary.NewOrdersToday);
        Assert.Equal(1, summary.OpenDisputes);
        Assert.Equal(1, summary.PendingRefunds);
        Assert.Equal(1, summary.PendingPayouts);
        Assert.Equal(0, summary.TotalGrossSalesPlaceholder);
        Assert.Equal(0, summary.PlatformCommissionPlaceholder);
    }

    private static async Task SeedDashboardDataAsync(AdminDashboardTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;

        var buyerId = Guid.NewGuid();
        var sellerUserId = Guid.NewGuid();
        var seller = new SellerProfile(sellerUserId);
        seller.UpdateProfile(
            "Seller Store",
            "seller-dashboard@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Seller Trading");
        var storefront = new SellerStorefront(seller.Id, "Seller Store", "seller-dashboard");
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payoutProfile = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-123");
        seller.SubmitForVerification(storefront, address, payoutProfile);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Pending review dress",
            "pending-review-dress",
            "Short description",
            "Full description");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);

        var order = new Order(buyerId, seller.Id, Guid.NewGuid(), now);
        var dispute = new Dispute(order.Id, null, buyerId, seller.Id, buyerId, "Item not as described.", now);
        var refund = new Refund(order.Id, Guid.NewGuid(), buyerId, seller.Id, null, 250m, "ZAR", "Refund review.", now);
        var payout = new SellerPayout(seller.Id, 875m, "ZAR", now);

        dbContext.AddRange(seller, storefront, address, payoutProfile, product, order, dispute, refund, payout);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-dashboard@example.test";
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
        AdminDashboardTestFactory factory,
        HttpClient client)
    {
        const string email = "admin-dashboard@example.test";

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

    private sealed class AdminDashboardTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminDashboardTests_{Guid.NewGuid():N}";

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
