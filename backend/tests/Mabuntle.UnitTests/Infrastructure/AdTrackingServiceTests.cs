using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Advertising;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Advertising;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class AdTrackingServiceTests
{
    [Fact]
    public async Task RecordImpressionAndClick_StoresEventsChargesAndMetrics()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCampaignAsync(dbContext);
        var service = new EfAdTrackingService(dbContext, TimeProvider.System);

        var impression = await service.RecordImpressionAsync(new TrackAdImpressionRequest(
            seed.Campaign.Id,
            seed.Product.Id,
            "shop-grid",
            "visitor-1"));
        var duplicateImpression = await service.RecordImpressionAsync(new TrackAdImpressionRequest(
            seed.Campaign.Id,
            seed.Product.Id,
            "shop-grid",
            "visitor-1"));
        var click = await service.RecordClickAsync(new TrackAdClickRequest(
            seed.Campaign.Id,
            seed.Product.Id,
            BuyerId: null,
            "visitor-1"));
        var duplicateClick = await service.RecordClickAsync(new TrackAdClickRequest(
            seed.Campaign.Id,
            seed.Product.Id,
            BuyerId: null,
            "visitor-1"));

        Assert.True(impression.Recorded);
        Assert.False(duplicateImpression.Recorded);
        Assert.True(click.Recorded);
        Assert.False(duplicateClick.Recorded);
        Assert.Equal(1, await dbContext.AdImpressions.CountAsync());
        Assert.Equal(1, await dbContext.AdClicks.CountAsync());
        Assert.Equal(1, await dbContext.AdCharges.CountAsync());

        var budget = await dbContext.AdBudgets.SingleAsync();
        Assert.Equal(5m, budget.SpentAmount);

        var metrics = await service.GetCampaignMetricsAsync(seed.Seller.Id, seed.Campaign.Id);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.Impressions);
        Assert.Equal(1, metrics.Clicks);
        Assert.Equal(1m, metrics.ClickThroughRate);
        Assert.Equal(5m, metrics.Spend);
    }

    [Fact]
    public async Task RecordClick_RequiresActiveCampaignAndSellableProduct()
    {
        await using var dbContext = CreateDbContext();
        var inactiveSeed = await SeedCampaignAsync(dbContext, active: false);
        var outOfStockSeed = await SeedCampaignAsync(dbContext, withStock: false);
        var service = new EfAdTrackingService(dbContext, TimeProvider.System);

        var inactiveResult = await service.RecordClickAsync(new TrackAdClickRequest(
            inactiveSeed.Campaign.Id,
            inactiveSeed.Product.Id,
            BuyerId: null,
            "visitor-1"));
        var outOfStockResult = await service.RecordClickAsync(new TrackAdClickRequest(
            outOfStockSeed.Campaign.Id,
            outOfStockSeed.Product.Id,
            BuyerId: null,
            "visitor-2"));

        Assert.False(inactiveResult.Recorded);
        Assert.Equal("CampaignNotActive", inactiveResult.Status);
        Assert.False(outOfStockResult.Recorded);
        Assert.Equal("ProductOutOfStock", outOfStockResult.Status);
        Assert.Empty(await dbContext.AdClicks.ToListAsync());
        Assert.Empty(await dbContext.AdCharges.ToListAsync());
    }

    [Fact]
    public async Task AttributeOrderConversionsAsync_StoresLatestBuyerClickForOrder()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedCampaignAsync(dbContext);
        var buyer = new BuyerProfile(Guid.NewGuid());
        var order = new Order(buyer.Id, seed.Seller.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var variant = await dbContext.ProductVariants.SingleAsync(item => item.ProductId == seed.Product.Id);
        order.AddItem(seed.Product.Id, variant.Id, seed.Product.Title, variant.Sku, variant.Size, variant.Colour, variant.Price, 2);
        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new EfAdTrackingService(dbContext, TimeProvider.System);
        await service.RecordClickAsync(new TrackAdClickRequest(
            seed.Campaign.Id,
            seed.Product.Id,
            buyer.Id,
            AnonymousVisitorId: null));

        await service.AttributeOrderConversionsAsync(order.Id);
        await service.AttributeOrderConversionsAsync(order.Id);

        var conversion = await dbContext.AdConversions.SingleAsync();
        Assert.Equal(seed.Campaign.Id, conversion.AdCampaignId);
        Assert.Equal(order.Id, conversion.OrderId);
        Assert.Equal(variant.Price * 2, conversion.RevenueAmount);

        var metrics = await service.GetCampaignMetricsAsync(seed.Seller.Id, seed.Campaign.Id);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.OrdersGenerated);
        Assert.Equal(variant.Price * 2, metrics.RevenueGenerated);
        Assert.True(metrics.ReturnOnAdSpend > 0);
    }

    private static async Task<(SellerProfile Seller, Product Product, AdCampaign Campaign)> SeedCampaignAsync(
        MabuntleDbContext dbContext,
        bool active = true,
        bool withStock = true)
    {
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
            withStock ? 10 : 0,
            status: withStock ? ProductVariantStatus.Active : ProductVariantStatus.OutOfStock));

        var campaign = new AdCampaign(
            seller.Id,
            "Launch campaign",
            AdCampaignType.FeaturedProduct,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(14),
            DateTimeOffset.UtcNow.AddDays(-1));
        campaign.ReplaceProducts([product.Id], DateTimeOffset.UtcNow.AddDays(-1));
        campaign.SubmitForReview(DateTimeOffset.UtcNow.AddDays(-1));
        if (active)
        {
            campaign.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));
        }

        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(new AdBudget(campaign.Id, "ZAR", 100m, 1000m, 5m, DateTimeOffset.UtcNow.AddDays(-1)));
        await dbContext.SaveChangesAsync();
        return (seller, product, campaign);
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

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"AdTrackingServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new MabuntleDbContext(options);
    }
}
