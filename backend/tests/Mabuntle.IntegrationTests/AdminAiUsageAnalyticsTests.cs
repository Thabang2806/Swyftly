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
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminAiUsageAnalyticsTests
{
    [Fact]
    public async Task Buyer_CannotAccessAiUsageAnalytics()
    {
        using var factory = new AdminAiUsageAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/analytics/ai-usage");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadAiUsageAnalyticsFilteredBySellerAndDateRange()
    {
        using var factory = new AdminAiUsageAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAiUsageDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync(
            $"/api/admin/analytics/ai-usage?fromUtc={Uri.EscapeDataString(seed.FromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(seed.ToUtc.ToString("O"))}&sellerId={seed.SellerId}");

        response.EnsureSuccessStatusCode();
        var analytics = await response.Content.ReadFromJsonAsync<AdminAiUsageAnalyticsResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(3, analytics!.Totals.Requests);
        Assert.Equal(2, analytics.Totals.SuccessfulRequests);
        Assert.Equal(1, analytics.Totals.FailedRequests);
        Assert.Equal(0.3333m, analytics.Totals.FailureRate);
        Assert.Equal(0.04m, analytics.Totals.EstimatedCost);
        Assert.Equal(150m, analytics.Totals.AverageLatencyMs);
        Assert.Equal(2, analytics.Suggestions.ProductSuggestionsGenerated);
        Assert.Equal(1, analytics.Suggestions.ProductSuggestionsAccepted);
        Assert.Equal(0.5m, analytics.Suggestions.SuggestionAcceptanceRate);
        Assert.Equal(1, analytics.Suggestions.ProductsImprovedWithAi);
        Assert.Equal(70m, analytics.Suggestions.AverageListingQualityScore);
        Assert.Null(analytics.Suggestions.AverageQualityScoreImprovement);
        Assert.Equal(1, analytics.Suggestions.FieldValuesAccepted);
        Assert.Equal(1, analytics.Suggestions.FieldValuesEdited);
        Assert.Equal(1, analytics.Moderation.ModerationChecks);
        Assert.Equal(1, analytics.Moderation.AdminReviewFlags);
        Assert.Equal(1, analytics.Moderation.HighRiskFlags);
        Assert.Contains(analytics.FeatureUsage, feature => feature.FeatureName == "ListingAssistant" && feature.Requests == 2);
        Assert.Contains(analytics.FeatureUsage, feature => feature.FeatureName == "ProductModeration" && feature.Requests == 1);

        var topSeller = Assert.Single(analytics.TopSellers);
        Assert.Equal(seed.SellerId, topSeller.SellerId);
        Assert.Equal("AI Seller", topSeller.SellerDisplayName);
    }

    [Fact]
    public async Task Admin_CanFilterAiUsageAnalyticsByFeature()
    {
        using var factory = new AdminAiUsageAnalyticsTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAiUsageDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await client.GetAsync(
            $"/api/admin/analytics/ai-usage?fromUtc={Uri.EscapeDataString(seed.FromUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(seed.ToUtc.ToString("O"))}&sellerId={seed.SellerId}&featureName=ListingAssistant");

        response.EnsureSuccessStatusCode();
        var analytics = await response.Content.ReadFromJsonAsync<AdminAiUsageAnalyticsResponse>();
        Assert.NotNull(analytics);
        Assert.Equal("ListingAssistant", analytics!.FeatureName);
        Assert.Equal(2, analytics.Totals.Requests);
        Assert.Equal(0, analytics.Moderation.ModerationChecks);
        Assert.Equal(2, analytics.Suggestions.ProductSuggestionsGenerated);
    }

    private static async Task<SeededAiUsageData> SeedAiUsageDataAsync(AdminAiUsageAnalyticsTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.UtcNow;
        var fromUtc = now.AddDays(-2);
        var toUtc = now.AddDays(2);
        var seller = CreateSeller("AI Seller", "ai-seller");
        var otherSeller = CreateSeller("Other AI Seller", "other-ai-seller");
        var productId = Guid.NewGuid();

        var appliedSuggestion = new AiProductSuggestion(
            seller.Id,
            productId,
            "Improve this listing.",
            "[]",
            "AI improved title",
            "AI short description",
            "AI full description",
            null,
            null,
            "{}",
            "[]",
            "[]",
            "[]",
            80m,
            "fake-model",
            "listing-v1",
            now);
        appliedSuggestion.Accept(now);
        appliedSuggestion.MarkApplied(now);

        var draftSuggestion = new AiProductSuggestion(
            seller.Id,
            Guid.NewGuid(),
            null,
            "[]",
            "Draft title",
            null,
            null,
            null,
            null,
            "{}",
            "[]",
            "[]",
            "[]",
            60m,
            "fake-model",
            "listing-v1",
            now);

        var otherSellerSuggestion = new AiProductSuggestion(
            otherSeller.Id,
            Guid.NewGuid(),
            null,
            "[]",
            "Other title",
            null,
            null,
            null,
            null,
            "{}",
            "[]",
            "[]",
            "[]",
            99m,
            "fake-model",
            "listing-v1",
            now);

        dbContext.AddRange(
            seller,
            otherSeller,
            new AiUsageLog("ListingAssistant", "seller-user", seller.Id, "fake-model", 100, 200, 0.01m, 100, true, null, now),
            new AiUsageLog("ListingAssistant", "seller-user", seller.Id, "fake-model", 100, 0, 0.02m, 300, false, "Provider timeout.", now),
            new AiUsageLog("ProductModeration", "seller-user", seller.Id, "moderation-model", 50, 10, 0.01m, 50, true, null, now),
            new AiUsageLog("ListingAssistant", "other-user", otherSeller.Id, "fake-model", 10, 20, 0.01m, 50, true, null, now),
            new AiUsageLog("ListingAssistant", "seller-user", seller.Id, "fake-model", 10, 20, 0.50m, 100, true, null, now.AddDays(-10)),
            appliedSuggestion,
            draftSuggestion,
            otherSellerSuggestion,
            new AiSuggestionFieldAudit(appliedSuggestion.Id, "title", "AI improved title", "AI improved title", wasAccepted: true, wasEdited: false, now),
            new AiSuggestionFieldAudit(appliedSuggestion.Id, "tags", "[\"ai\"]", "[\"ai\",\"seller\"]", wasAccepted: false, wasEdited: true, now),
            new AiModerationResult(productId, seller.Id, AiModerationRiskLevel.High, true, "High-risk language.", "[]", "[]", "[\"risk\"]", "local", now),
            new AiModerationResult(Guid.NewGuid(), otherSeller.Id, AiModerationRiskLevel.Low, false, "Low risk.", "[]", "[]", "[]", "local", now));

        await dbContext.SaveChangesAsync();
        return new SeededAiUsageData(fromUtc, toUtc, seller.Id);
    }

    private static SellerProfile CreateSeller(string displayName, string slugPrefix)
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            displayName,
            $"{slugPrefix}@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            $"{displayName} Trading");
        return seller;
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        var email = $"buyer-ai-analytics-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", MabuntleRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminAiUsageAnalyticsTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-ai-analytics-{Guid.NewGuid():N}@example.test";

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

    private sealed record SeededAiUsageData(DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid SellerId);

    private sealed class AdminAiUsageAnalyticsTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminAiUsageAnalyticsTests_{Guid.NewGuid():N}";

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
