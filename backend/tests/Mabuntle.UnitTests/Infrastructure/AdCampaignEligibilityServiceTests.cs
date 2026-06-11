using Microsoft.EntityFrameworkCore;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Advertising;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class AdCampaignEligibilityServiceTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsEligible_ForVerifiedSellerAndPublishedInStockProduct()
    {
        await using var dbContext = CreateDbContext();
        var seller = CreateVerifiedSeller();
        var product = CreatePublishedProduct(seller.Id);
        SeedEligibleProduct(dbContext, product);
        dbContext.SellerProfiles.Add(seller);
        await dbContext.SaveChangesAsync();
        var service = new AdCampaignEligibilityService(dbContext);

        var result = await service.ValidateAsync(seller.Id, [product.Id]);

        Assert.True(result.IsEligible);
        var productResult = Assert.Single(result.Products);
        Assert.True(productResult.IsEligible);
        Assert.True(productResult.QualityScore >= AdCampaignEligibilityService.MinimumProductQualityScore);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnverifiedSeller()
    {
        await using var dbContext = CreateDbContext();
        var seller = new SellerProfile(Guid.NewGuid());
        var product = CreatePublishedProduct(seller.Id);
        SeedEligibleProduct(dbContext, product);
        dbContext.SellerProfiles.Add(seller);
        await dbContext.SaveChangesAsync();
        var service = new AdCampaignEligibilityService(dbContext);

        var result = await service.ValidateAsync(seller.Id, [product.Id]);

        Assert.False(result.IsEligible);
        Assert.Contains(result.SellerReasons, reason => reason.Contains("verified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_RejectsProductWithoutSellableStock()
    {
        await using var dbContext = CreateDbContext();
        var seller = CreateVerifiedSeller();
        var product = CreatePublishedProduct(seller.Id);
        dbContext.SellerProfiles.Add(seller);
        dbContext.Products.Add(product);
        dbContext.ProductImages.Add(new ProductImage(product.Id, "https://example.test/image.jpg", "image.jpg", null, 0, true, DateTimeOffset.UtcNow));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
        await dbContext.SaveChangesAsync();
        var service = new AdCampaignEligibilityService(dbContext);

        var result = await service.ValidateAsync(seller.Id, [product.Id]);

        Assert.False(result.IsEligible);
        Assert.Contains(Assert.Single(result.Products).Reasons, reason => reason.Contains("in-stock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_RejectsProductWithModerationFlags()
    {
        await using var dbContext = CreateDbContext();
        var seller = CreateVerifiedSeller();
        var product = CreatePublishedProduct(seller.Id);
        SeedEligibleProduct(dbContext, product);
        dbContext.SellerProfiles.Add(seller);
        dbContext.AiModerationResults.Add(new AiModerationResult(
            product.Id,
            seller.Id,
            AiModerationRiskLevel.High,
            needsAdminReview: true,
            "Counterfeit risk.",
            "[]",
            "[]",
            "[\"counterfeit\"]",
            "LocalRules",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        var service = new AdCampaignEligibilityService(dbContext);

        var result = await service.ValidateAsync(seller.Id, [product.Id]);

        Assert.False(result.IsEligible);
        Assert.Contains(Assert.Single(result.Products).Reasons, reason => reason.Contains("moderation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_RejectsSellerWithSeriousDispute()
    {
        await using var dbContext = CreateDbContext();
        var seller = CreateVerifiedSeller();
        var product = CreatePublishedProduct(seller.Id);
        SeedEligibleProduct(dbContext, product);
        dbContext.SellerProfiles.Add(seller);
        var dispute = new Dispute(Guid.NewGuid(), null, Guid.NewGuid(), seller.Id, Guid.NewGuid(), "Review required.", DateTimeOffset.UtcNow);
        dispute.MarkUnderAdminReview(DateTimeOffset.UtcNow);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();
        var service = new AdCampaignEligibilityService(dbContext);

        var result = await service.ValidateAsync(seller.Id, [product.Id]);

        Assert.False(result.IsEligible);
        Assert.Contains(result.SellerReasons, reason => reason.Contains("serious dispute", StringComparison.OrdinalIgnoreCase));
    }

    private static void SeedEligibleProduct(MabuntleDbContext dbContext, Product product)
    {
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, null, 10));
        dbContext.ProductImages.Add(new ProductImage(product.Id, "https://example.test/image.jpg", "image.jpg", null, 0, true, DateTimeOffset.UtcNow));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
    }

    private static SellerProfile CreateVerifiedSeller()
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile("Seller Store", "seller@example.test", "+27110000000", SellerBusinessType.RegisteredBusiness, "Seller Trading");
        var storefront = new SellerStorefront(seller.Id, "Seller Store", "seller-store");
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, "provider-ref");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);
        return seller;
    }

    private static Product CreatePublishedProduct(Guid sellerId)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(Guid.NewGuid(), null, "Cotton Dress", "cotton-dress", "A cotton dress.", "A breathable cotton dress for summer.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);
        return product;
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"AdCampaignEligibilityServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new MabuntleDbContext(options);
    }
}
