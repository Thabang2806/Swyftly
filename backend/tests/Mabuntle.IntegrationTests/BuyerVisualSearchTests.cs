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
using Mabuntle.Api.Ai;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Buyers;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class BuyerVisualSearchTests
{
    [Fact]
    public async Task Seller_CannotUseBuyerVisualSearch()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        var sellerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Seller);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("black dress image", null, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VisualSearch_ReturnsOnlyRealPublishedProductIdsFromSearchResults()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("black formal maxi dress flatlay", null, "black-dress.jpg", "image/jpeg"));

        response.EnsureSuccessStatusCode();
        var visualSearch = await response.Content.ReadFromJsonAsync<BuyerVisualSearchResponse>();
        Assert.NotNull(visualSearch);
        var product = Assert.Single(visualSearch!.Products);
        Assert.Equal(productId, product.ProductId);
        Assert.Equal("Dresses", visualSearch.Attributes.Category);
        Assert.Contains(product.MatchReasons, reason => reason.Contains("Black", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("not persisted", visualSearch.ImageRetentionNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VisualSearch_ReturnsEmptyResultsWithoutInventingProducts()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        _ = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("silver hoop earrings", null, null, null));

        response.EnsureSuccessStatusCode();
        var visualSearch = await response.Content.ReadFromJsonAsync<BuyerVisualSearchResponse>();
        Assert.NotNull(visualSearch);
        Assert.Empty(visualSearch!.Products);
        Assert.Contains("No published in-stock products matched", visualSearch.Summary);
    }

    [Fact]
    public async Task VisualSearch_RejectsMissingImageInput()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest(null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VisualSearch_SavesSafeHistoryOnlyWhenBuyerEnabledPreference()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var updatePreferenceResponse = await client.PutAsJsonAsync(
            "/api/buyer/ai-discovery/preferences",
            new BuyerAiDiscoveryPreferenceRequest(true));
        updatePreferenceResponse.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("black formal maxi dress flatlay", null, "black-dress.jpg", "image/jpeg"));
        response.EnsureSuccessStatusCode();

        using var historyResponse = await client.GetAsync("/api/buyer/ai-discovery/history?tool=VisualSearch");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<BuyerAiDiscoveryHistoryListResponse>();
        Assert.NotNull(history);
        var item = Assert.Single(history!.Items);
        Assert.Equal("VisualSearch", item.SourceTool);
        Assert.Equal("Dresses", item.Category);
        Assert.Equal("Black", item.Colour);
        Assert.Contains(productId, item.ProductIds);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var storedHistory = Assert.Single(await dbContext.BuyerAiDiscoveryHistory.AsNoTracking().ToListAsync());
        Assert.Equal(BuyerGrowthSourceTool.VisualSearch, storedHistory.SourceTool);
        Assert.DoesNotContain("black-dress.jpg", string.Join(" ", storedHistory.Category, storedHistory.Colour, storedHistory.Material, storedHistory.SourceRoute));
    }

    [Fact]
    public async Task VisualSearch_AddsPersonalizationReasonsOnlyAfterBuyerEnablesPreference()
    {
        using var factory = new BuyerVisualSearchTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var buyerId = await dbContext.BuyerProfiles.Select(buyer => buyer.Id).SingleAsync();
            dbContext.BuyerWishlistItems.Add(new BuyerWishlistItem(buyerId, productId, DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var disabledResponse = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("black formal maxi dress flatlay", null, "black-dress.jpg", "image/jpeg"));
        disabledResponse.EnsureSuccessStatusCode();
        var disabledVisualSearch = await disabledResponse.Content.ReadFromJsonAsync<BuyerVisualSearchResponse>();
        Assert.NotNull(disabledVisualSearch);
        Assert.False(Assert.Single(disabledVisualSearch!.Products).PersonalizationApplied);

        using var updatePreferenceResponse = await client.PutAsJsonAsync(
            "/api/buyer/ai-discovery/preferences",
            new BuyerAiDiscoveryPreferenceRequest(HistoryEnabled: false, PersonalizationEnabled: true));
        updatePreferenceResponse.EnsureSuccessStatusCode();

        using var enabledResponse = await client.PostAsJsonAsync(
            "/api/buyer/ai/visual-search",
            new BuyerVisualSearchRequest("black formal maxi dress flatlay", null, "black-dress.jpg", "image/jpeg"));
        enabledResponse.EnsureSuccessStatusCode();
        var enabledVisualSearch = await enabledResponse.Content.ReadFromJsonAsync<BuyerVisualSearchResponse>();
        Assert.NotNull(enabledVisualSearch);
        var product = Assert.Single(enabledVisualSearch!.Products);
        Assert.Equal(productId, product.ProductId);
        Assert.True(product.PersonalizationApplied);
        Assert.Contains("Similar to saved items", product.PersonalizationReasons);
    }

    private static async Task<Guid> SeedPublishedDressAsync(BuyerVisualSearchTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Visual Seller",
            "visual-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Visual Seller Trading");
        var category = new Category(Guid.NewGuid(), null, "Dresses", $"visual-dresses-{Guid.NewGuid():N}");
        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            category.Id,
            null,
            "Black Formal Maxi Dress",
            $"black-formal-maxi-dress-{Guid.NewGuid():N}",
            "A black formal maxi dress.",
            "A plain black formal maxi dress for evening events.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.AddRange(
            seller,
            category,
            product,
            new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", 999m, null, 5),
            new ProductAttributeValue(product.Id, "style", "\"Formal\""),
            new ProductAttributeValue(product.Id, "shape", "\"Maxi\""),
            new ProductAttributeValue(product.Id, "pattern", "\"Plain\""),
            new ProductImage(
                product.Id,
                "https://example.test/black-formal-maxi-dress.jpg",
                $"products/{product.Id:N}/primary.jpg",
                "Black formal maxi dress",
                0,
                isPrimary: true,
                DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}-visual-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private sealed class BuyerVisualSearchTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleBuyerVisualSearchTests_{Guid.NewGuid():N}";

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
