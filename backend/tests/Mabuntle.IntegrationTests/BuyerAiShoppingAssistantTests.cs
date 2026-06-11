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

public sealed class BuyerAiShoppingAssistantTests
{
    [Fact]
    public async Task Seller_CannotUseBuyerShoppingAssistant()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var sellerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Seller);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("black dress"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BuyerShoppingAssistant_ReturnsOnlyRealPublishedProductIdsFromSearchResults()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));

        response.EnsureSuccessStatusCode();
        var assistant = await response.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(assistant);
        var product = Assert.Single(assistant!.Products);
        Assert.Equal(productId, product.ProductId);
        Assert.Contains(product.MatchReasons, reason => reason.Contains("Black", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Dresses", assistant.Intent.Category);
        Assert.Equal("M", assistant.Intent.Size);
        Assert.Equal(1500m, assistant.Intent.BudgetMax);
    }

    [Fact]
    public async Task BuyerShoppingAssistant_ReturnsEmptyResultsWithoutInventingProducts()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        _ = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Find gold earrings for sensitive ears under R300."));

        response.EnsureSuccessStatusCode();
        var assistant = await response.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(assistant);
        Assert.Empty(assistant!.Products);
        Assert.Contains("No exact products matched", assistant.Summary);
    }

    [Fact]
    public async Task BuyerAiHistory_IsDisabledByDefaultAndDoesNotStoreAssistantPromptData()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        _ = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var preferenceResponse = await client.GetAsync("/api/buyer/ai-discovery/preferences");
        preferenceResponse.EnsureSuccessStatusCode();
        var preference = await preferenceResponse.Content.ReadFromJsonAsync<BuyerAiDiscoveryPreferenceResponse>();
        Assert.NotNull(preference);
        Assert.False(preference!.HistoryEnabled);
        Assert.False(preference.PersonalizationEnabled);
        Assert.Null(preference.UpdatedAtUtc);

        using var response = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Empty(await dbContext.BuyerAiDiscoveryHistory.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task BuyerShoppingAssistant_PersonalizesOnlyAfterBuyerEnablesPreference()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var (cheaperProductId, savedProductId) = await SeedPublishedDressPairAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
            var buyerId = await dbContext.BuyerProfiles.Select(buyer => buyer.Id).SingleAsync();
            dbContext.BuyerWishlistItems.Add(new BuyerWishlistItem(buyerId, savedProductId, DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var disabledResponse = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));
        disabledResponse.EnsureSuccessStatusCode();
        var disabledAssistant = await disabledResponse.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(disabledAssistant);
        Assert.Equal(cheaperProductId, disabledAssistant!.Products.First().ProductId);
        Assert.All(disabledAssistant.Products, product => Assert.False(product.PersonalizationApplied));

        using var updatePreferenceResponse = await client.PutAsJsonAsync(
            "/api/buyer/ai-discovery/preferences",
            new BuyerAiDiscoveryPreferenceRequest(HistoryEnabled: false, PersonalizationEnabled: true));
        updatePreferenceResponse.EnsureSuccessStatusCode();

        using var enabledResponse = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));
        enabledResponse.EnsureSuccessStatusCode();
        var enabledAssistant = await enabledResponse.Content.ReadFromJsonAsync<BuyerAiShoppingAssistantResponse>();
        Assert.NotNull(enabledAssistant);
        var firstProduct = enabledAssistant!.Products.First();
        Assert.Equal(savedProductId, firstProduct.ProductId);
        Assert.True(firstProduct.PersonalizationApplied);
        Assert.Contains("Similar to saved items", firstProduct.PersonalizationReasons);
    }

    [Fact]
    public async Task BuyerAiHistory_EnabledBuyerCanListAndDeleteOnlyOwnHistory()
    {
        using var factory = new BuyerAiShoppingAssistantTestFactory();
        using var client = factory.CreateClient();
        var productId = await SeedPublishedDressAsync(factory);
        var buyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);
        var otherBuyerToken = await RegisterAndLoginAsync(client, MabuntleRoles.Buyer);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var updatePreferenceResponse = await client.PutAsJsonAsync(
            "/api/buyer/ai-discovery/preferences",
            new BuyerAiDiscoveryPreferenceRequest(true));
        updatePreferenceResponse.EnsureSuccessStatusCode();

        using var assistantResponse = await client.PostAsJsonAsync(
            "/api/buyer/ai/shopping-assistant",
            new BuyerAiShoppingAssistantRequest("Show me a black dress in size medium under R1,500."));
        assistantResponse.EnsureSuccessStatusCode();

        using var historyResponse = await client.GetAsync("/api/buyer/ai-discovery/history?tool=Assistant");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<BuyerAiDiscoveryHistoryListResponse>();
        Assert.NotNull(history);
        var item = Assert.Single(history!.Items);
        Assert.Equal("Assistant", item.SourceTool);
        Assert.Equal("Dresses", item.Category);
        Assert.Equal("Black", item.Colour);
        Assert.Contains(productId, item.ProductIds);
        Assert.Contains(item.Products, product => product.ProductId == productId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var storedHistory = Assert.Single(await dbContext.BuyerAiDiscoveryHistory.AsNoTracking().ToListAsync());
        Assert.Equal(BuyerGrowthSourceTool.Assistant, storedHistory.SourceTool);
        Assert.DoesNotContain("Show me", string.Join(" ", storedHistory.Category, storedHistory.Colour, storedHistory.Material, storedHistory.SourceRoute));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherBuyerToken);
        using var otherHistoryResponse = await client.GetAsync("/api/buyer/ai-discovery/history");
        otherHistoryResponse.EnsureSuccessStatusCode();
        var otherHistory = await otherHistoryResponse.Content.ReadFromJsonAsync<BuyerAiDiscoveryHistoryListResponse>();
        Assert.NotNull(otherHistory);
        Assert.Empty(otherHistory!.Items);

        using var otherDeleteResponse = await client.DeleteAsync($"/api/buyer/ai-discovery/history/{item.HistoryId}");
        Assert.Equal(HttpStatusCode.NotFound, otherDeleteResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var deleteResponse = await client.DeleteAsync($"/api/buyer/ai-discovery/history/{item.HistoryId}");
        deleteResponse.EnsureSuccessStatusCode();

        Assert.Empty(await dbContext.BuyerAiDiscoveryHistory.AsNoTracking().ToListAsync());
    }

    private static async Task<Guid> SeedPublishedDressAsync(BuyerAiShoppingAssistantTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Assistant Seller",
            "assistant-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Assistant Seller Trading");
        var category = new Category(Guid.NewGuid(), null, "Dresses", $"assistant-dresses-{Guid.NewGuid():N}");
        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            category.Id,
            null,
            "Black Wedding Dress",
            $"black-wedding-dress-{Guid.NewGuid():N}",
            "A black dress for formal occasions.",
            "A black dress suitable for weddings and evening events.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.AddRange(
            seller,
            category,
            product,
            new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", 999m, null, 5),
            new ProductImage(
                product.Id,
                "https://example.test/black-dress.jpg",
                $"products/{product.Id:N}/primary.jpg",
                "Black wedding dress",
                0,
                isPrimary: true,
                DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<(Guid CheaperProductId, Guid SavedProductId)> SeedPublishedDressPairAsync(BuyerAiShoppingAssistantTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Personalized Assistant Seller",
            "personalized-assistant-seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Personalized Assistant Seller Trading");
        var category = new Category(Guid.NewGuid(), null, "Dresses", $"personalized-assistant-dresses-{Guid.NewGuid():N}");

        var cheaperProduct = CreatePublishedDress(seller.Id, category.Id, "Black Day Dress", 999m);
        var savedProduct = CreatePublishedDress(seller.Id, category.Id, "Black Evening Dress", 1200m);

        dbContext.AddRange(
            seller,
            category,
            cheaperProduct.Product,
            cheaperProduct.Variant,
            cheaperProduct.Image,
            savedProduct.Product,
            savedProduct.Variant,
            savedProduct.Image);
        await dbContext.SaveChangesAsync();
        return (cheaperProduct.Product.Id, savedProduct.Product.Id);
    }

    private static (Product Product, ProductVariant Variant, ProductImage Image) CreatePublishedDress(
        Guid sellerId,
        Guid categoryId,
        string title,
        decimal price)
    {
        var product = new Product(sellerId);
        product.UpdateDraftDetails(
            categoryId,
            null,
            title,
            $"{title.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            "A black dress for formal occasions.",
            "A black dress suitable for weddings and evening events.");
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);

        return (
            product,
            new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", price, null, 5),
            new ProductImage(
                product.Id,
                $"https://example.test/{product.Slug}.jpg",
                $"products/{product.Id:N}/primary.jpg",
                title,
                0,
                isPrimary: true,
                DateTimeOffset.UtcNow));
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}-assistant-{Guid.NewGuid():N}@example.test";
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

    private sealed class BuyerAiShoppingAssistantTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleBuyerAiShoppingAssistantTests_{Guid.NewGuid():N}";

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
