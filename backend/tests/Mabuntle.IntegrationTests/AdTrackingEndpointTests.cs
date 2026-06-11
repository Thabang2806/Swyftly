using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Advertising;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdTrackingEndpointTests
{
    [Fact]
    public async Task ImpressionWithoutVisitorId_DedupesByRequestFingerprint()
    {
        await using var factory = new AdTrackingEndpointTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MabuntleIntegrationTests/1.0");
        var seed = await SeedCampaignAsync(factory);
        var request = new TrackAdImpressionApiRequest(
            seed.CampaignId,
            seed.ProductId,
            "shop-grid",
            AnonymousVisitorId: null);

        using var firstResponse = await client.PostAsJsonAsync("/api/ads/impressions", request);
        using var secondResponse = await client.PostAsJsonAsync("/api/ads/impressions", request);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var impression = await dbContext.AdImpressions.SingleAsync();
        Assert.StartsWith("fp_", impression.AnonymousVisitorId, StringComparison.Ordinal);
    }

    private static async Task<(Guid CampaignId, Guid ProductId)> SeedCampaignAsync(AdTrackingEndpointTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var seller = CreateVerifiedSeller();
        var product = CreatePublishedProduct(seller.Id);
        dbContext.SellerProfiles.Add(seller);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499m,
            null,
            10,
            status: ProductVariantStatus.Active));

        var now = DateTimeOffset.UtcNow.AddDays(-1);
        var campaign = new AdCampaign(
            seller.Id,
            "Launch campaign",
            AdCampaignType.FeaturedProduct,
            now,
            DateTimeOffset.UtcNow.AddDays(14),
            now);
        campaign.ReplaceProducts([product.Id], now);
        campaign.SubmitForReview(now);
        campaign.Approve(Guid.NewGuid(), now);

        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(new AdBudget(campaign.Id, "ZAR", 100m, 1000m, 5m, now));
        await dbContext.SaveChangesAsync();
        return (campaign.Id, product.Id);
    }

    private static SellerProfile CreateVerifiedSeller()
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile("Seller Store", "seller@example.test", "+27110000000", SellerBusinessType.RegisteredBusiness, "Seller Trading");
        var storefront = new SellerStorefront(seller.Id, "Seller Store", $"seller-store-{Guid.NewGuid():N}");
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);
        return seller;
    }

    private static Product CreatePublishedProduct(Guid sellerId)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(Guid.NewGuid(), null, $"Cotton Dress {Guid.NewGuid():N}", $"cotton-dress-{Guid.NewGuid():N}", "A cotton dress.", "A breathable cotton dress for summer.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow.AddDays(-1));
        return product;
    }

    private sealed class AdTrackingEndpointTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdTrackingEndpointTests_{Guid.NewGuid():N}";

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
