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
using Swyftly.Api.Admin;
using Swyftly.Api.Advertising;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AdminAdCampaignReviewTests
{
    [Fact]
    public async Task Admin_CanListPendingAdCampaigns()
    {
        using var factory = new AdminAdCampaignReviewTestFactory();
        using var client = factory.CreateClient();
        var campaign = await CreatePendingCampaignAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/ad-campaigns/pending");

        response.EnsureSuccessStatusCode();
        var campaigns = await response.Content.ReadFromJsonAsync<AdminAdCampaignSummaryResponse[]>();
        Assert.NotNull(campaigns);
        var pendingCampaign = Assert.Single(campaigns!, item => item.AdCampaignId == campaign.AdCampaignId);
        Assert.Equal("PendingReview", pendingCampaign.Status);
        Assert.Equal("Review Seller", pendingCampaign.SellerDisplayName);
    }

    [Fact]
    public async Task Admin_CanListOperationalAdCampaigns_WithAllStateFilters()
    {
        using var factory = new AdminAdCampaignReviewTestFactory();
        using var client = factory.CreateClient();
        var campaign = await CreatePendingCampaignAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var needsAttentionResponse = await client.GetAsync("/api/admin/ad-campaigns");
        needsAttentionResponse.EnsureSuccessStatusCode();
        var needsAttention = await needsAttentionResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>>();
        Assert.NotNull(needsAttention);
        var pendingCampaign = Assert.Single(needsAttention!.Items, item => item.AdCampaignId == campaign.AdCampaignId);
        Assert.Equal("PendingReview", pendingCampaign.Status);
        Assert.Equal($"/admin/ads/{campaign.AdCampaignId}", pendingCampaign.DetailRoute);

        using var approveResponse = await client.PostAsJsonAsync(
            $"/api/admin/ad-campaigns/{campaign.AdCampaignId}/approve",
            new { });
        approveResponse.EnsureSuccessStatusCode();

        using var allStateResponse = await client.GetAsync("/api/admin/ad-campaigns?view=All&status=Active&search=Launch&page=1&pageSize=10&sort=UpdatedDesc");
        allStateResponse.EnsureSuccessStatusCode();
        var allState = await allStateResponse.Content.ReadFromJsonAsync<AdminPagedResponse<AdminAdCampaignOperationalSummaryResponse>>();
        Assert.NotNull(allState);
        var activeCampaign = Assert.Single(allState!.Items, item => item.AdCampaignId == campaign.AdCampaignId);
        Assert.Equal("Active", activeCampaign.Status);
        Assert.Contains(allState.StatusCounts, count => count.Status == "Active" && count.Count >= 1);
    }

    [Fact]
    public async Task Approve_ChangesCampaignToActive_AndWritesAuditLog()
    {
        using var factory = new AdminAdCampaignReviewTestFactory();
        using var client = factory.CreateClient();
        var campaign = await CreatePendingCampaignAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/ad-campaigns/{campaign.AdCampaignId}/approve",
            new { });

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminAdCampaignDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Active", detail!.Status);
        Assert.Contains(detail.AuditTrail, entry => entry.ActionType == "AdCampaignApproved");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Equal(1, await dbContext.AuditLogs.CountAsync(auditLog => auditLog.ActionType == "AdCampaignApproved"));
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.AdCampaignApproved
            && notification.RelatedEntityId == campaign.AdCampaignId));
    }

    [Fact]
    public async Task Reject_RequiresReason_AndWritesAuditLog()
    {
        using var factory = new AdminAdCampaignReviewTestFactory();
        using var client = factory.CreateClient();
        var campaign = await CreatePendingCampaignAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var missingReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/ad-campaigns/{campaign.AdCampaignId}/reject",
            new AdminAdCampaignReasonRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, missingReasonResponse.StatusCode);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/ad-campaigns/{campaign.AdCampaignId}/reject",
            new AdminAdCampaignReasonRequest("Promoted products do not meet ad policy."));

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<AdminAdCampaignDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Rejected", detail!.Status);
        Assert.Contains(
            detail.AuditTrail,
            entry => entry.ActionType == "AdCampaignRejected" && entry.Reason == "Promoted products do not meet ad policy.");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.True(await dbContext.Notifications.AnyAsync(notification =>
            notification.Type == SellerNotificationTypes.AdCampaignRejected
            && notification.RelatedEntityId == campaign.AdCampaignId));
    }

    [Fact]
    public async Task Approve_RechecksProductEligibility()
    {
        using var factory = new AdminAdCampaignReviewTestFactory();
        using var client = factory.CreateClient();
        var campaign = await CreatePendingCampaignAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
            var variant = await dbContext.ProductVariants.SingleAsync(item => item.ProductId == campaign.ProductId);
            variant.MarkOutOfStock();
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/ad-campaigns/{campaign.AdCampaignId}/approve",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scopeAfter = factory.Services.CreateScope();
        var dbContextAfter = scopeAfter.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var savedCampaign = await dbContextAfter.AdCampaigns.SingleAsync(item => item.Id == campaign.AdCampaignId);
        Assert.Equal(AdCampaignStatus.PendingReview, savedCampaign.Status);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminAdCampaignReviewTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-ads-{Guid.NewGuid():N}@example.test";

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

            var roleResult = await userManager.AddToRoleAsync(admin, SwyftlyRoles.Admin);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task<(Guid AdCampaignId, Guid ProductId)> CreatePendingCampaignAsync(
        AdminAdCampaignReviewTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Review Seller",
            "review-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Review Seller Trading");

        var storefront = new SellerStorefront(seller.Id, "Review Seller", $"review-seller-{Guid.NewGuid():N}");
        var address = new SellerAddress(
            seller.Id,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref-ads");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            CatalogSeedData.WomenDresses,
            null,
            "Sponsored Summer Dress",
            $"sponsored-summer-dress-{Guid.NewGuid():N}",
            "A lightweight summer dress.",
            "A lightweight summer dress with a relaxed fit for warm weather.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        var campaign = new AdCampaign(
            seller.Id,
            "Launch campaign",
            AdCampaignType.FeaturedProduct,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(14),
            DateTimeOffset.UtcNow);
        campaign.ReplaceProducts([product.Id], DateTimeOffset.UtcNow);
        campaign.SubmitForReview(DateTimeOffset.UtcNow);

        dbContext.SellerProfiles.Add(seller);
        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        dbContext.Products.Add(product);
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499.99m,
            699.99m,
            10));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            "https://example.test/sponsored-summer-dress.jpg",
            $"products/{product.Id:N}/primary.jpg",
            "Sponsored summer dress",
            0,
            isPrimary: true,
            DateTimeOffset.UtcNow));
        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(new AdBudget(campaign.Id, "ZAR", 100m, 1000m, 5m, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        return (campaign.Id, product.Id);
    }

    private sealed class AdminAdCampaignReviewTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAdminAdCampaignReviewTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();

                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
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
