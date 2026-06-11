using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
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
using Mabuntle.Api.Buyers;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Common;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminMarketplaceReportTests
{
    [Fact]
    public async Task Buyer_CannotAccessMarketplaceReport()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/reports/marketplace");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadMarketplaceReportFilteredByDateRange()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReportDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync(
            $"/api/admin/reports/marketplace?fromUtc={Uri.EscapeDataString(seed.FromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(seed.ToUtc.ToString("O"))}");

        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<AdminMarketplaceReportResponse>();
        Assert.NotNull(report);
        Assert.Equal(120m, report!.Finance.GrossMerchandiseValue);
        Assert.Equal(12m, report.Finance.PlatformCommissionEarned);
        Assert.Equal(4m, report.Finance.PaymentProcessingFees);
        Assert.Equal(30m, report.Finance.Refunds);
        Assert.Equal(100m, report.Finance.SellerPendingBalances);
        Assert.Equal(600m, report.Finance.SellerAvailableBalances);
        Assert.Equal(25m, report.Finance.SellerHeldBalances);
        Assert.Equal(80m, report.Finance.PayoutsProcessed);
        Assert.Equal(45m, report.Finance.FailedPayouts);
        Assert.Equal(1, report.Operations.OrderCount);
        Assert.Equal(1, report.Operations.RefundCount);
        Assert.Equal(1, report.Operations.PayoutsProcessedCount);
        Assert.Equal(1, report.Operations.FailedPayoutCount);
        Assert.Equal(1, report.Operations.DisputeCount);
        Assert.Equal(1, report.Operations.ActiveDisputeCount);

        var topSeller = Assert.Single(report.TopSellers);
        Assert.Equal(seed.SellerId, topSeller.SellerId);
        Assert.Equal("Report Seller", topSeller.SellerDisplayName);
        Assert.Equal(2, topSeller.ItemsSold);

        var topCategory = Assert.Single(report.TopCategories);
        Assert.Equal(seed.CategoryId, topCategory.CategoryId);
        Assert.Equal("Report Dresses", topCategory.CategoryName);
        Assert.Equal(120m, topCategory.Revenue);
    }

    [Fact]
    public async Task Admin_CanExportMarketplaceReportCsv()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReportDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync(
            $"/api/admin/reports/marketplace/export.csv?fromUtc={Uri.EscapeDataString(seed.FromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(seed.ToUtc.ToString("O"))}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"finance\",\"grossMerchandiseValue\",\"120\",\"ZAR\"", csv);
        Assert.Contains("\"operations\",\"orderCount\",\"1\",\"\"", csv);
    }

    [Fact]
    public async Task Buyer_CanRecordGrowthEventWithoutRawPromptData()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedBuyerGrowthProductAsync(factory);
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/growth-events",
            new BuyerGrowthEventRequest(
                "AssistantProductOpened",
                "Assistant",
                productId,
                3,
                "High",
                "Dresses",
                "Black",
                "Linen",
                "/assistant",
                null));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var growthEvent = Assert.Single(await dbContext.BuyerGrowthEvents.AsNoTracking().ToListAsync());
        Assert.Equal(BuyerGrowthEventType.AssistantProductOpened, growthEvent.EventType);
        Assert.Equal(BuyerGrowthSourceTool.Assistant, growthEvent.SourceTool);
        Assert.Equal(productId, growthEvent.ProductId);
        Assert.Equal("Dresses", growthEvent.Category);
    }

    [Fact]
    public async Task Admin_CannotWriteBuyerGrowthEvent()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/growth-events",
            new BuyerGrowthEventRequest(
                "AssistantSearchSubmitted",
                "Assistant",
                null,
                0,
                null,
                null,
                null,
                null,
                "/assistant",
                null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BuyerGrowthEvent_RejectsInvalidValues()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/growth-events",
            new BuyerGrowthEventRequest(
                "UnknownEvent",
                "Assistant",
                null,
                -1,
                "Certain",
                new string('x', 101),
                null,
                null,
                "/assistant",
                "FreeText"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadBuyerGrowthReportWithoutBuyerIdentity()
    {
        using var factory = new AdminMarketplaceReportTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedBuyerGrowthReportDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync(
            $"/api/admin/reports/buyer-growth?fromUtc={Uri.EscapeDataString(seed.FromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(seed.ToUtc.ToString("O"))}&bucket=Day");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(seed.BuyerId.ToString(), json, StringComparison.OrdinalIgnoreCase);

        var report = JsonSerializer.Deserialize<AdminBuyerGrowthReportResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(report);
        Assert.Equal(2, report!.Summary.SearchSubmittedCount);
        Assert.Equal(1, report.Summary.ShopHandoffCount);
        Assert.Equal(1, report.Summary.ProductOpenedCount);
        Assert.Equal(1, report.Summary.FeedbackSubmittedCount);
        Assert.Contains(report.TopCategories, category => category.Value == "Dresses" && category.Count == 5);
        Assert.Contains(report.ConfidenceBreakdown, confidence => confidence.Name == "High");
        Assert.NotEmpty(report.Trend);
    }

    private static async Task<SeededReportData> SeedReportDataAsync(AdminMarketplaceReportTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;
        var fromUtc = now.AddDays(-2);
        var toUtc = now.AddDays(2);
        var buyerId = Guid.NewGuid();

        var seller = CreateVerifiedSeller("Report Seller", "report-seller");
        var otherSeller = CreateVerifiedSeller("Other Seller", "other-report-seller");
        var category = new Category(Guid.NewGuid(), null, "Report Dresses", $"report-dresses-{Guid.NewGuid():N}");
        var product = new Product(seller.Profile.Id);
        product.UpdateDraftDetails(
            category.Id,
            null,
            "Report dress",
            $"report-dress-{Guid.NewGuid():N}",
            "Dress for report tests.",
            "Dress for report tests.");
        var otherProduct = new Product(otherSeller.Profile.Id);
        otherProduct.UpdateDraftDetails(
            category.Id,
            null,
            "Out of range dress",
            $"out-of-range-dress-{Guid.NewGuid():N}",
            "Out of range test product.",
            "Out of range test product.");

        var order = new Order(buyerId, seller.Profile.Id, Guid.NewGuid(), now);
        order.AddItem(product.Id, Guid.NewGuid(), product.Title, "SKU-REPORT-1", "M", "Black", 60m, 2);
        order.ChangeStatus(OrderStatus.Paid, now, "Payment captured.");

        var outOfRangeOrder = new Order(buyerId, otherSeller.Profile.Id, Guid.NewGuid(), now.AddDays(-10));
        outOfRangeOrder.AddItem(otherProduct.Id, Guid.NewGuid(), otherProduct.Title, "SKU-REPORT-2", "M", "Blue", 500m, 1);

        var refund = new Refund(order.Id, Guid.NewGuid(), buyerId, seller.Profile.Id, null, 30m, "ZAR", "Report refund.", now);
        refund.Approve(Guid.NewGuid(), "Approved.", now);
        refund.MarkProcessing(now);
        refund.MarkRefunded("refund-ref", now);

        var dispute = new Dispute(order.Id, null, buyerId, seller.Profile.Id, buyerId, "Report dispute.", now);
        var balance = new SellerBalance(seller.Profile.Id, "ZAR");
        balance.CreditPending(125m);
        balance.HoldPending(25m);
        SetPrivateProperty(balance, nameof(SellerBalance.AvailableBalance), 600m);

        var processedPayout = new SellerPayout(seller.Profile.Id, 80m, "ZAR", now);
        SetPrivateProperty(processedPayout, nameof(SellerPayout.Status), SellerPayoutStatus.PaidOut);
        var failedPayout = new SellerPayout(seller.Profile.Id, 45m, "ZAR", now);
        SetPrivateProperty(failedPayout, nameof(SellerPayout.Status), SellerPayoutStatus.Failed);

        dbContext.AddRange(
            seller.Profile,
            seller.Storefront,
            seller.Address,
            seller.PayoutProfile,
            otherSeller.Profile,
            otherSeller.Storefront,
            otherSeller.Address,
            otherSeller.PayoutProfile,
            category,
            product,
            otherProduct,
            order,
            outOfRangeOrder,
            refund,
            dispute,
            balance,
            processedPayout,
            failedPayout,
            new LedgerEntry(order.Id, null, seller.Profile.Id, buyerId, null, LedgerEntryType.PlatformCommissionRecorded, 12m, "ZAR", LedgerDirection.Credit, "Commission.", now),
            new LedgerEntry(order.Id, null, seller.Profile.Id, buyerId, null, LedgerEntryType.PaymentProviderFeeRecorded, 4m, "ZAR", LedgerDirection.Debit, "Provider fee.", now),
            new LedgerEntry(outOfRangeOrder.Id, null, otherSeller.Profile.Id, buyerId, null, LedgerEntryType.PlatformCommissionRecorded, 50m, "ZAR", LedgerDirection.Credit, "Out of range.", now.AddDays(-10)));

        await dbContext.SaveChangesAsync();

        dbContext.Entry(outOfRangeOrder).Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now.AddDays(-10);
        dbContext.Entry(outOfRangeOrder).Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now.AddDays(-10);
        dbContext.Entry(outOfRangeOrder).Property(nameof(AuditableEntity.CreatedAtUtc)).IsModified = true;
        dbContext.Entry(outOfRangeOrder).Property(nameof(AuditableEntity.UpdatedAtUtc)).IsModified = true;
        await dbContext.SaveChangesAsync();

        return new SeededReportData(fromUtc, toUtc, seller.Profile.Id, category.Id);
    }

    private static async Task<Guid> SeedBuyerGrowthProductAsync(AdminMarketplaceReportTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var seller = CreateVerifiedSeller("Growth Seller", "growth-seller");
        var product = new Product(seller.Profile.Id);
        product.UpdateDraftDetails(
            null,
            null,
            "Growth telemetry dress",
            $"growth-telemetry-dress-{Guid.NewGuid():N}",
            "Dress for telemetry tests.",
            "Dress for telemetry tests.");

        dbContext.AddRange(seller.Profile, seller.Storefront, seller.Address, seller.PayoutProfile, product);
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<SeededBuyerGrowthReportData> SeedBuyerGrowthReportDataAsync(AdminMarketplaceReportTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;
        var buyer = new BuyerProfile(Guid.NewGuid());
        var seller = CreateVerifiedSeller("Growth Report Seller", "growth-report-seller");
        var product = new Product(seller.Profile.Id);
        product.UpdateDraftDetails(
            null,
            null,
            "Growth report dress",
            $"growth-report-dress-{Guid.NewGuid():N}",
            "Dress for growth report tests.",
            "Dress for growth report tests.");

        dbContext.AddRange(
            buyer,
            seller.Profile,
            seller.Storefront,
            seller.Address,
            seller.PayoutProfile,
            product,
            new BuyerGrowthEvent(
                buyer.Id,
                BuyerGrowthEventType.AssistantSearchSubmitted,
                BuyerGrowthSourceTool.Assistant,
                now.AddHours(-4),
                null,
                2,
                BuyerGrowthConfidenceBand.High,
                "Dresses",
                "Black",
                "Linen",
                "/assistant"),
            new BuyerGrowthEvent(
                buyer.Id,
                BuyerGrowthEventType.VisualSearchSubmitted,
                BuyerGrowthSourceTool.VisualSearch,
                now.AddHours(-3),
                null,
                1,
                BuyerGrowthConfidenceBand.Low,
                "Dresses",
                "Black",
                null,
                "/visual-search"),
            new BuyerGrowthEvent(
                buyer.Id,
                BuyerGrowthEventType.AssistantProductOpened,
                BuyerGrowthSourceTool.Assistant,
                now.AddHours(-2),
                product.Id,
                2,
                BuyerGrowthConfidenceBand.High,
                "Dresses",
                "Black",
                "Linen",
                "/assistant"),
            new BuyerGrowthEvent(
                buyer.Id,
                BuyerGrowthEventType.AssistantShopHandoff,
                BuyerGrowthSourceTool.Assistant,
                now.AddHours(-1),
                null,
                2,
                BuyerGrowthConfidenceBand.High,
                "Dresses",
                "Black",
                "Linen",
                "/assistant"),
            new BuyerGrowthEvent(
                buyer.Id,
                BuyerGrowthEventType.AssistantFeedbackSubmitted,
                BuyerGrowthSourceTool.Assistant,
                now,
                null,
                2,
                BuyerGrowthConfidenceBand.High,
                "Dresses",
                "Black",
                "Linen",
                "/assistant",
                BuyerGrowthFeedbackReason.GoodMatches));

        await dbContext.SaveChangesAsync();
        return new SeededBuyerGrowthReportData(now.AddDays(-1), now.AddDays(1), buyer.Id);
    }

    private static SeededSeller CreateVerifiedSeller(string displayName, string slugPrefix)
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            displayName,
            $"{slugPrefix}@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            $"{displayName} Trading");
        var storefront = new SellerStorefront(seller.Id, displayName, $"{slugPrefix}-{Guid.NewGuid():N}");
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payoutProfile = new SellerPayoutProfilePlaceholder(seller.Id, $"provider-{Guid.NewGuid():N}");
        payoutProfile.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payoutProfile);
        return new SeededSeller(seller, storefront, address, payoutProfile);
    }

    private static void SetPrivateProperty<T>(object instance, string propertyName, T value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        var email = $"buyer-report-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", MabuntleRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminMarketplaceReportTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-report-{Guid.NewGuid():N}@example.test";

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

    private sealed record SeededReportData(DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid SellerId, Guid CategoryId);

    private sealed record SeededBuyerGrowthReportData(DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid BuyerId);

    private sealed record SeededSeller(
        SellerProfile Profile,
        SellerStorefront Storefront,
        SellerAddress Address,
        SellerPayoutProfilePlaceholder PayoutProfile);

    private sealed class AdminMarketplaceReportTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminMarketplaceReportTests_{Guid.NewGuid():N}";

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
