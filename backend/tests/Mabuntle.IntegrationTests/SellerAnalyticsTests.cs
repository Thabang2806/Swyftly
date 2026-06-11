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
using Mabuntle.Api.Analytics;
using Mabuntle.Api.Authentication;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Notifications;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Domain.Support;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class SellerAnalyticsTests
{
    [Fact]
    public async Task Buyer_CannotAccessSellerAnalytics()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "analytics-buyer@example.test", MabuntleRoles.Buyer);
        var token = await LoginAsync(client, "analytics-buyer@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/seller/analytics/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StorefrontAnalytics_RecordsAnonymousProductViewAndDedupesByIdempotency()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-public-seller@example.test", verify: true);
        Guid productId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var product = CreatePublishedProduct(seller.SellerId, "Public Funnel Product");
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();
            productId = product.Id;
        }

        var payload = new
        {
            eventType = "ProductViewed",
            productId,
            anonymousVisitorId = "visitor_12345",
            sourceRoute = "/product/public-funnel-product",
            idempotencyKey = "product-view-public-funnel",
            utmSource = "newsletter",
            utmMedium = "email",
            utmCampaign = "launch",
            referrerHost = "mail.example.test",
            sourceCategory = "Email"
        };

        using var first = await client.PostAsJsonAsync("/api/analytics/storefront-events", payload);
        using var second = await client.PostAsJsonAsync("/api/analytics/storefront-events", payload);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await verifyContext.SellerFunnelEvents.CountAsync(
            funnelEvent => funnelEvent.SellerId == seller.SellerId
                && funnelEvent.ProductId == productId
                && funnelEvent.EventType == SellerFunnelEventType.ProductViewed
                && funnelEvent.SourceCategory == "Email"
                && funnelEvent.UtmSource == "newsletter"
                && funnelEvent.ReferrerHost == "mail.example.test"));
    }

    [Fact]
    public async Task StorefrontAnalytics_RejectsMalformedVisitorIdAndUnknownProduct()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();

        using var malformed = await client.PostAsJsonAsync("/api/analytics/storefront-events", new
        {
            eventType = "ProductViewed",
            productId = Guid.NewGuid(),
            anonymousVisitorId = "bad visitor id",
            sourceRoute = "/product/missing"
        });
        using var unknownProduct = await client.PostAsJsonAsync("/api/analytics/storefront-events", new
        {
            eventType = "ProductViewed",
            productId = Guid.NewGuid(),
            anonymousVisitorId = "visitor_12345",
            sourceRoute = "/product/missing"
        });
        using var invalidSource = await client.PostAsJsonAsync("/api/analytics/storefront-events", new
        {
            eventType = "ProductViewed",
            productId = Guid.NewGuid(),
            anonymousVisitorId = "visitor_12345",
            sourceRoute = "/product/missing",
            sourceCategory = "Telepathy"
        });

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownProduct.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSource.StatusCode);
    }

    [Fact]
    public async Task SellerAnalytics_ReturnsOnlyAuthenticatedSellerMetrics()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await CreateSellerUserAsync(factory, client, "analytics-seller-one@example.test");
        var sellerTwo = await CreateSellerUserAsync(factory, client, "analytics-seller-two@example.test");
        await SeedAnalyticsDataAsync(factory, sellerOne.SellerId, sellerTwo.SellerId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);

        using var response = await client.GetAsync("/api/seller/analytics/summary");

        response.EnsureSuccessStatusCode();
        var analytics = await response.Content.ReadFromJsonAsync<SellerAnalyticsSummaryResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(sellerOne.SellerId, analytics!.SellerId);
        Assert.Equal(998m, analytics.TotalSales);
        Assert.Equal(1, analytics.OrderCount);
        Assert.Equal(2, analytics.ProductsSold);
        Assert.Equal(100m, analytics.TotalRefunded);
        Assert.Equal(1m, analytics.RefundRate);
        Assert.Equal(1m, analytics.ReturnRate);
        Assert.DoesNotContain(analytics.TopProducts, product => product.ProductTitle == "Other Seller Product");
        Assert.Contains(analytics.TopProducts, product => product.ProductTitle == "Seller One Product");
        Assert.Contains(analytics.LowStockProducts, product => product.Title == "Seller One Product");
        Assert.Equal(1, analytics.AdPerformance.CampaignCount);
        Assert.Equal(1, analytics.AdPerformance.Impressions);
        Assert.Equal(1, analytics.AdPerformance.Clicks);
        Assert.Equal(1, analytics.AdPerformance.OrdersGenerated);
        Assert.Equal(0.5m, analytics.ConversionRatePlaceholder);
        Assert.Equal(3, analytics.AiUsage.Requests);
        Assert.Equal(2, analytics.AiUsage.SuccessfulRequests);
        Assert.Equal(1, analytics.AiUsage.FailedRequests);
    }

    [Fact]
    public async Task SellerAnalyticsPerformance_ReturnsSellerScopedTrendAndBreakdowns()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await CreateSellerUserAsync(factory, client, "analytics-performance-one@example.test");
        var sellerTwo = await CreateSellerUserAsync(factory, client, "analytics-performance-two@example.test");
        await SeedAnalyticsDataAsync(factory, sellerOne.SellerId, sellerTwo.SellerId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var toUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        using var response = await client.GetAsync($"/api/seller/analytics/performance?fromUtc={fromUtc}&toUtc={toUtc}&bucket=Day");

        response.EnsureSuccessStatusCode();
        var analytics = await response.Content.ReadFromJsonAsync<SellerAnalyticsPerformanceResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(sellerOne.SellerId, analytics!.SellerId);
        Assert.Equal("Day", analytics.Bucket);
        Assert.Equal(1, analytics.SalesTrend.Sum(bucket => bucket.OrderCount));
        Assert.Equal(998m, analytics.SalesTrend.Sum(bucket => bucket.GrossSales));
        Assert.Equal(100m, analytics.SalesTrend.Sum(bucket => bucket.RefundedAmount));
        Assert.Contains(analytics.ProductPerformance, product =>
            product.ProductTitle == "Seller One Product"
            && product.UnitsSold == 2
            && product.ReturnCount == 1
            && product.StockQuantity == 3);
        Assert.DoesNotContain(analytics.ProductPerformance, product => product.ProductTitle == "Other Seller Product");
        Assert.Contains(analytics.InventoryPerformance, item =>
            item.Sku == "SKU-1"
            && item.Barcode == "BARCODE-1"
            && item.IsLowStock);
        Assert.Single(analytics.AdPerformance);
        Assert.Equal(1, analytics.CustomerCareSummary.ReturnCount);
        Assert.Equal(1, analytics.CustomerCareSummary.RefundCount);
        Assert.Equal(1, analytics.CustomerCareSummary.SupportTicketCount);
        Assert.Equal(1, analytics.CustomerCareSummary.DisputeCount);
        Assert.Equal(2, analytics.FunnelSummary.ProductViews);
        Assert.Equal(1, analytics.FunnelSummary.AddToCartCount);
        Assert.Equal(1, analytics.FunnelSummary.PaidOrderCount);
        Assert.Contains(analytics.SourceBreakdown, source =>
            source.SourceCategory == "Email"
            && source.ProductViews == 1
            && source.TopUtmSource == "newsletter");
        Assert.Contains(analytics.ProductFunnel, product =>
            product.ProductTitle == "Seller One Product"
            && product.ProductViews == 2
            && product.AddToCartCount == 1
            && product.DominantSourceCategory == "Email");

        using var filteredResponse = await client.GetAsync($"/api/seller/analytics/performance?fromUtc={fromUtc}&toUtc={toUtc}&bucket=Day&sourceCategory=Email");
        filteredResponse.EnsureSuccessStatusCode();
        var filteredAnalytics = await filteredResponse.Content.ReadFromJsonAsync<SellerAnalyticsPerformanceResponse>();
        Assert.NotNull(filteredAnalytics);
        Assert.Equal(1, filteredAnalytics!.FunnelSummary.ProductViews);
        Assert.All(filteredAnalytics.ProductFunnel, product => Assert.Equal("Email", product.DominantSourceCategory));
    }

    [Fact]
    public async Task SellerAnalyticsExport_ReturnsCsvForRequestedReport()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var sellerOne = await CreateSellerUserAsync(factory, client, "analytics-export-one@example.test");
        var sellerTwo = await CreateSellerUserAsync(factory, client, "analytics-export-two@example.test");
        await SeedAnalyticsDataAsync(factory, sellerOne.SellerId, sellerTwo.SellerId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerOne.AccessToken);
        var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var toUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        using var response = await client.GetAsync($"/api/seller/analytics/export.csv?report=Products&fromUtc={fromUtc}&toUtc={toUtc}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"productId\",\"productTitle\",\"productSlug\"", csv);
        Assert.Contains("Seller One Product", csv);
        Assert.DoesNotContain("Other Seller Product", csv);

        using var funnelResponse = await client.GetAsync($"/api/seller/analytics/export.csv?report=Funnel&fromUtc={fromUtc}&toUtc={toUtc}");
        funnelResponse.EnsureSuccessStatusCode();
        var funnelCsv = await funnelResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"productId\",\"productTitle\",\"productSlug\",\"dominantSourceCategory\"", funnelCsv);
        Assert.Contains("Seller One Product", funnelCsv);
        Assert.Contains("newsletter", funnelCsv);
        Assert.DoesNotContain("Other Seller Product", funnelCsv);
    }

    [Fact]
    public async Task SellerAnalyticsPerformance_RejectsInvalidFilters()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-invalid@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);
        var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var toUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        using var badRange = await client.GetAsync($"/api/seller/analytics/performance?fromUtc={fromUtc}&toUtc={toUtc}");
        using var badBucket = await client.GetAsync("/api/seller/analytics/performance?bucket=Month");
        using var badReport = await client.GetAsync("/api/seller/analytics/export.csv?report=Unknown");
        using var badSource = await client.GetAsync("/api/seller/analytics/performance?sourceCategory=UnknownChannel");

        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badBucket.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badReport.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badSource.StatusCode);
    }

    [Fact]
    public async Task SellerAnalyticsReportSchedule_CanBeReadAndUpdatedByVerifiedSeller()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-schedule@example.test", verify: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var getResponse = await client.GetAsync("/api/seller/analytics/report-schedule");
        getResponse.EnsureSuccessStatusCode();
        var defaultSchedule = await getResponse.Content.ReadFromJsonAsync<SellerReportScheduleResponse>();
        Assert.NotNull(defaultSchedule);
        Assert.False(defaultSchedule!.IsEnabled);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/seller/analytics/report-schedule",
            new SellerReportScheduleRequest(
                IsEnabled: true,
                Frequency: "Weekly",
                ReportRange: "Last30Days",
                SendDayOfWeek: "Monday",
                SendDayOfMonth: null,
                SendTimeLocal: "08:00",
                TimeZoneId: "Africa/Johannesburg"));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<SellerReportScheduleResponse>();
        Assert.NotNull(updated);
        Assert.True(updated!.IsEnabled);
        Assert.Equal("Weekly", updated.Frequency);
        Assert.NotNull(updated.NextRunAtUtc);
    }

    [Fact]
    public async Task SellerAnalyticsReportSchedule_RejectsUnverifiedSellerAndInvalidPayload()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-schedule-unverified@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var conflict = await client.PutAsJsonAsync(
            "/api/seller/analytics/report-schedule",
            new SellerReportScheduleRequest(true, "Weekly", "Last30Days", "Monday", null, "08:00", "Africa/Johannesburg"));
        using var invalid = await client.PutAsJsonAsync(
            "/api/seller/analytics/report-schedule",
            new SellerReportScheduleRequest(false, "Daily", "Last30Days", null, null, "8am", "Unknown/Zone"));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task SellerAnalyticsReportSchedule_SendTestQueuesSellerNotificationAndEmail()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-schedule-test@example.test", verify: true);
        await SeedAnalyticsDataAsync(factory, seller.SellerId, Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seller.AccessToken);

        using var response = await client.PostAsJsonAsync(
            "/api/seller/analytics/report-schedule/send-test",
            new { });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SellerReportDigestSendResult>();
        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var notification = await dbContext.Notifications.SingleAsync(
            item => item.RecipientUserId == seller.UserId
                && item.Type == SellerNotificationTypes.SellerAnalyticsDigestReady);
        Assert.True(notification.IsInAppVisible);
        Assert.Contains("gross sales", notification.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.NotificationEmailDeliveries.CountAsync(
            delivery => delivery.NotificationId == notification.Id));
    }

    [Fact]
    public async Task SellerScheduledReportProcessor_SendsDueReportsOnce()
    {
        using var factory = new SellerAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seller = await CreateSellerUserAsync(factory, client, "analytics-schedule-worker@example.test", verify: true);
        await SeedAnalyticsDataAsync(factory, seller.SellerId, Guid.NewGuid());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;
        var schedule = new SellerReportSchedule(seller.SellerId, now.AddDays(-1));
        schedule.Update(
            true,
            SellerReportFrequency.Weekly,
            SellerReportRange.Last7Days,
            DayOfWeek.Monday,
            null,
            "08:00",
            "Africa/Johannesburg",
            now.AddMinutes(-1),
            now.AddDays(-1));
        dbContext.SellerReportSchedules.Add(schedule);
        await dbContext.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<ISellerScheduledReportService>();
        var first = await service.ProcessDueReportsAsync(now);
        var second = await service.ProcessDueReportsAsync(now.AddSeconds(5));

        Assert.Equal(1, first.ProcessedCount);
        Assert.Equal(1, first.SentCount);
        Assert.Equal(0, second.ProcessedCount);
        Assert.Equal(1, await dbContext.SellerReportScheduleRuns.CountAsync(run => run.SellerId == seller.SellerId));
        Assert.Equal(1, await dbContext.Notifications.CountAsync(
            notification => notification.RecipientUserId == seller.UserId
                && notification.Type == SellerNotificationTypes.SellerAnalyticsDigestReady));
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<(Guid SellerId, Guid UserId, string AccessToken)> CreateSellerUserAsync(
        SellerAnalyticsTestFactory factory,
        HttpClient client,
        string email,
        bool verify = false)
    {
        await RegisterAsync(client, email, MabuntleRoles.Seller);
        var token = await LoginAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var sellerId = await dbContext.SellerProfiles
            .Where(seller => seller.UserId == user!.Id)
            .Select(seller => seller.Id)
            .SingleAsync();

        if (verify)
        {
            var seller = await dbContext.SellerProfiles.SingleAsync(existing => existing.Id == sellerId);
            VerifySeller(dbContext, seller);
            await dbContext.SaveChangesAsync();
        }

        return (sellerId, user!.Id, token);
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

    private static async Task SeedAnalyticsDataAsync(
        SellerAnalyticsTestFactory factory,
        Guid sellerOneId,
        Guid sellerTwoId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var buyerId = Guid.NewGuid();
        var sellerOneProduct = CreatePublishedProduct(sellerOneId, "Seller One Product");
        var sellerTwoProduct = CreatePublishedProduct(sellerTwoId, "Other Seller Product");
        var sellerOneVariant = new ProductVariant(sellerOneProduct.Id, "SKU-1", "M", "Black", 499m, null, 3, barcode: "BARCODE-1");
        var sellerTwoVariant = new ProductVariant(sellerTwoProduct.Id, "SKU-2", "M", "Black", 999m, null, 10, barcode: "BARCODE-2");

        var sellerOneOrder = new Order(buyerId, sellerOneId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        sellerOneOrder.AddItem(sellerOneProduct.Id, sellerOneVariant.Id, sellerOneProduct.Title, sellerOneVariant.Sku, sellerOneVariant.Size, sellerOneVariant.Colour, sellerOneVariant.Price, 2);
        sellerOneOrder.ChangeStatus(OrderStatus.Paid, DateTimeOffset.UtcNow.AddDays(-1), "PaymentConfirmed");
        var sellerTwoOrder = new Order(Guid.NewGuid(), sellerTwoId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        sellerTwoOrder.AddItem(sellerTwoProduct.Id, sellerTwoVariant.Id, sellerTwoProduct.Title, sellerTwoVariant.Sku, sellerTwoVariant.Size, sellerTwoVariant.Colour, sellerTwoVariant.Price, 1);
        sellerTwoOrder.ChangeStatus(OrderStatus.Paid, DateTimeOffset.UtcNow.AddDays(-1), "PaymentConfirmed");

        var returnRequest = new ReturnRequest(
            sellerOneOrder.Id,
            buyerId,
            sellerOneId,
            ReturnReason.DamagedItem,
            "Damaged on arrival.",
            DateTimeOffset.UtcNow);
        returnRequest.AddItem(
            sellerOneOrder.Items.Single().Id,
            sellerOneProduct.Id,
            sellerOneVariant.Id,
            1,
            ReturnReason.DamagedItem,
            isOpenedOrUnsealed: false,
            "Box crushed.");
        returnRequest.MarkAwaitingSellerResponse(DateTimeOffset.UtcNow);
        var refund = new Refund(sellerOneOrder.Id, Guid.NewGuid(), buyerId, sellerOneId, returnRequest.Id, 100m, "ZAR", "Return approved.", DateTimeOffset.UtcNow);
        refund.Approve(Guid.NewGuid(), "Approved.", DateTimeOffset.UtcNow);
        refund.MarkProcessing(DateTimeOffset.UtcNow);
        refund.MarkRefunded("fake_refund", DateTimeOffset.UtcNow);

        var campaign = new AdCampaign(
            sellerOneId,
            "Launch campaign",
            AdCampaignType.FeaturedProduct,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow.AddDays(-1));
        campaign.ReplaceProducts([sellerOneProduct.Id], DateTimeOffset.UtcNow.AddDays(-1));
        campaign.SubmitForReview(DateTimeOffset.UtcNow.AddDays(-1));
        campaign.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));
        var click = new AdClick(campaign.Id, sellerOneProduct.Id, buyerId, null, DateTimeOffset.UtcNow);
        var supportTicket = new SupportTicket(
            Guid.NewGuid(),
            "Seller",
            null,
            sellerOneId,
            SupportTicketCategory.OrderIssue,
            "Order dispatch question",
            "Need help with dispatch.",
            sellerOneOrder.Id,
            sellerOneProduct.Id,
            sellerOneId,
            null,
            DateTimeOffset.UtcNow);
        var dispute = new Dispute(
            sellerOneOrder.Id,
            returnRequest.Id,
            buyerId,
            sellerOneId,
            buyerId,
            "Return evidence dispute.",
            DateTimeOffset.UtcNow);

        dbContext.Products.AddRange(sellerOneProduct, sellerTwoProduct);
        dbContext.ProductVariants.AddRange(sellerOneVariant, sellerTwoVariant);
        dbContext.Orders.AddRange(sellerOneOrder, sellerTwoOrder);
        dbContext.ReturnRequests.Add(returnRequest);
        dbContext.Refunds.Add(refund);
        dbContext.InventoryMovements.Add(new InventoryMovement(
            sellerOneId,
            sellerOneProduct.Id,
            sellerOneVariant.Id,
            InventoryMovementType.SellerAdjustment,
            2,
            3,
            0,
            0,
            ProductVariantStatus.Active,
            ProductVariantStatus.Active,
            "TestSeed",
            "Seed low-stock movement.",
            null,
            null,
            DateTimeOffset.UtcNow));
        dbContext.SupportTickets.Add(supportTicket);
        dbContext.Disputes.Add(dispute);
        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(new AdBudget(campaign.Id, "ZAR", 100m, 1000m, 5m, DateTimeOffset.UtcNow));
        dbContext.AdImpressions.Add(new AdImpression(campaign.Id, sellerOneProduct.Id, "shop-grid", "visitor-1", DateTimeOffset.UtcNow));
        dbContext.AdClicks.Add(click);
        dbContext.AdCharges.Add(new AdCharge(campaign.Id, click.Id, 5m, "ZAR", "Click", DateTimeOffset.UtcNow));
        dbContext.AdConversions.Add(new AdConversion(campaign.Id, click.Id, sellerOneOrder.Id, sellerOneOrder.TotalAmount, "ZAR", DateTimeOffset.UtcNow));
        dbContext.SellerFunnelEvents.AddRange(
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.StorefrontViewed,
                DateTimeOffset.UtcNow.AddDays(-2),
                hashedAnonymousVisitorId: "visitor-one-hash",
                sourceRoute: "/seller/store-one",
                idempotencyKey: "storefront-view-one",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.ProductViewed,
                DateTimeOffset.UtcNow.AddDays(-2),
                productId: sellerOneProduct.Id,
                hashedAnonymousVisitorId: "visitor-one-hash",
                sourceRoute: "/product/seller-one-product",
                idempotencyKey: "product-view-one",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.ProductViewed,
                DateTimeOffset.UtcNow.AddDays(-1),
                productId: sellerOneProduct.Id,
                hashedAnonymousVisitorId: "visitor-two-hash",
                sourceRoute: "/product/seller-one-product",
                idempotencyKey: "product-view-two",
                referrerHost: "google.com",
                sourceCategory: "Search"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.ProductAddedToCart,
                DateTimeOffset.UtcNow.AddDays(-1),
                productId: sellerOneProduct.Id,
                hashedAnonymousVisitorId: "visitor-one-hash",
                sourceRoute: "/product/seller-one-product",
                idempotencyKey: "cart-one",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.CheckoutStarted,
                DateTimeOffset.UtcNow.AddHours(-12),
                cartId: sellerOneOrder.CartId,
                buyerId: buyerId,
                hashedAnonymousVisitorId: "visitor-one-hash",
                sourceRoute: "/checkout",
                idempotencyKey: "checkout-one",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.OrderCreated,
                DateTimeOffset.UtcNow.AddHours(-10),
                cartId: sellerOneOrder.CartId,
                orderId: sellerOneOrder.Id,
                buyerId: buyerId,
                sourceRoute: "server",
                idempotencyKey: $"OrderCreated:{sellerOneOrder.Id:N}",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerOneId,
                SellerFunnelEventType.OrderPaid,
                DateTimeOffset.UtcNow.AddHours(-8),
                cartId: sellerOneOrder.CartId,
                orderId: sellerOneOrder.Id,
                buyerId: buyerId,
                sourceRoute: "server",
                idempotencyKey: $"OrderPaid:{sellerOneOrder.Id:N}",
                utmSource: "newsletter",
                utmMedium: "email",
                utmCampaign: "winter-edit",
                referrerHost: "mail.example.test",
                sourceCategory: "Email"),
            new SellerFunnelEvent(
                sellerTwoId,
                SellerFunnelEventType.ProductViewed,
                DateTimeOffset.UtcNow.AddDays(-1),
                productId: sellerTwoProduct.Id,
                hashedAnonymousVisitorId: "other-visitor-hash",
                sourceRoute: "/product/other-seller-product",
                idempotencyKey: "other-product-view",
                sourceCategory: "Direct"));
        dbContext.AiUsageLogs.AddRange(
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 20, 0.01m, 100, true, null, DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 20, 0.01m, 110, true, null, DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "seller-user", sellerOneId, "fake-model", 10, 0, 0.005m, 50, false, "Invalid response.", DateTimeOffset.UtcNow),
            new AiUsageLog("ListingAssistant", "other-user", sellerTwoId, "fake-model", 10, 20, 0.01m, 100, true, null, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static Product CreatePublishedProduct(Guid sellerId, string title)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(Guid.NewGuid(), null, title, $"{title.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}", "Short description.", "Full product description.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow.AddDays(-3));
        return product;
    }

    private sealed class SellerAnalyticsTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleSellerAnalyticsTests_{Guid.NewGuid():N}";

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
