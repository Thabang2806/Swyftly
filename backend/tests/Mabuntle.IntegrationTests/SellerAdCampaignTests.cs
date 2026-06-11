using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Advertising;
using Mabuntle.Api.Authentication;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class SellerAdCampaignTests
{
    private const string TestPassword = "Password123!";

    [Fact]
    public async Task VerifiedSeller_CanCreateDraftCampaign_AndSubmitForReview()
    {
        using var factory = new SellerAdCampaignTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, "ads-seller@example.test", MabuntleRoles.Seller);
        var (sellerId, productId) = await VerifySellerAndCreateProductAsync(factory, auth.UserId);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/seller/ad-campaigns",
            CreateRequest(productId));
        createResponse.EnsureSuccessStatusCode();
        var campaign = await createResponse.Content.ReadFromJsonAsync<SellerAdCampaignResponse>();
        Assert.NotNull(campaign);
        Assert.Equal(sellerId, campaign!.SellerId);
        Assert.Equal("Draft", campaign.Status);
        Assert.True(campaign.Eligibility.IsEligible);

        using var submitResponse = await client.PostAsync($"/api/seller/ad-campaigns/{campaign.AdCampaignId}/submit-review", null);
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SellerAdCampaignResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("PendingReview", submitted!.Status);
    }

    [Fact]
    public async Task Seller_CannotCreateCampaignForIneligibleProduct()
    {
        using var factory = new SellerAdCampaignTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAndLoginAsync(client, "ads-ineligible-seller@example.test", MabuntleRoles.Seller);
        var (_, productId) = await VerifySellerAndCreateProductAsync(factory, auth.UserId, withStock: false);

        using var response = await client.PostAsJsonAsync(
            "/api/seller/ad-campaigns",
            CreateRequest(productId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seller_CannotReadAnotherSellersCampaign()
    {
        using var factory = new SellerAdCampaignTestFactory();
        using var sellerOneClient = factory.CreateClient();
        using var sellerTwoClient = factory.CreateClient();
        var sellerOneAuth = await RegisterAndLoginAsync(sellerOneClient, "ads-owner@example.test", MabuntleRoles.Seller);
        var sellerTwoAuth = await RegisterAndLoginAsync(sellerTwoClient, "ads-other@example.test", MabuntleRoles.Seller);
        var (_, productId) = await VerifySellerAndCreateProductAsync(factory, sellerOneAuth.UserId);
        await VerifySellerAndCreateProductAsync(factory, sellerTwoAuth.UserId);

        using var createResponse = await sellerOneClient.PostAsJsonAsync(
            "/api/seller/ad-campaigns",
            CreateRequest(productId));
        createResponse.EnsureSuccessStatusCode();
        var campaign = await createResponse.Content.ReadFromJsonAsync<SellerAdCampaignResponse>();
        Assert.NotNull(campaign);

        using var forbiddenResponse = await sellerTwoClient.GetAsync($"/api/seller/ad-campaigns/{campaign!.AdCampaignId}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
    }

    private static UpsertSellerAdCampaignRequest CreateRequest(Guid productId) =>
        new(
            "Launch campaign",
            "FeaturedProduct",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(14),
            [productId],
            new UpsertAdBudgetRequest("ZAR", 100m, 1000m, 5m));

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string email, string role)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, TestPassword, role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private static async Task<(Guid SellerId, Guid ProductId)> VerifySellerAndCreateProductAsync(
        SellerAdCampaignTestFactory factory,
        Guid sellerUserId,
        bool withStock = true)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var seller = await dbContext.SellerProfiles.SingleAsync(item => item.UserId == sellerUserId);
        seller.UpdateProfile("Seller Store", "seller@example.test", "+27110000000", SellerBusinessType.RegisteredBusiness, "Seller Trading");
        var storefront = new SellerStorefront(seller.Id, $"Store {seller.Id:N}"[..20], $"store-{seller.Id:N}"[..24]);
        var address = new SellerAddress(seller.Id, "1 Market Street", null, "Johannesburg", "Gauteng", "2000", "ZA");
        var payout = new SellerPayoutProfilePlaceholder(seller.Id, $"provider-{seller.Id:N}");
        payout.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);
        seller.MarkVerified(storefront, address, payout);

        var product = new Product(seller.Id);
        product.UpdateDraftDetails(Guid.NewGuid(), null, $"Ad Product {Guid.NewGuid():N}", $"ad-product-{Guid.NewGuid():N}", "Short description", "Full product description for advertising.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.SellerStorefronts.Add(storefront);
        dbContext.SellerAddresses.Add(address);
        dbContext.SellerPayoutProfiles.Add(payout);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(new ProductVariant(
            product.Id,
            $"SKU-{Guid.NewGuid():N}",
            "M",
            "Black",
            499m,
            null,
            withStock ? 10 : 0));
        dbContext.ProductImages.Add(new ProductImage(
            product.Id,
            "https://example.test/ad-product.jpg",
            $"ad-product-{Guid.NewGuid():N}.jpg",
            null,
            0,
            true,
            DateTimeOffset.UtcNow));
        dbContext.ProductAttributeValues.Add(new ProductAttributeValue(product.Id, "material", "\"Cotton\""));
        await dbContext.SaveChangesAsync();

        return (seller.Id, product.Id);
    }

    private sealed class SellerAdCampaignTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleSellerAdCampaignTests_{Guid.NewGuid():N}";

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
