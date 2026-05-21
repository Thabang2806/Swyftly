using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swyftly.Api.Authentication;
using Swyftly.Api.Catalog;
using Swyftly.Application.Identity;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AdminCategoryTests
{
    [Fact]
    public async Task Buyer_CannotListAdminCategories()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/categories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListSeededCategoriesAndAttributes()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/categories");

        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<AdminCategoryResponse[]>();
        Assert.NotNull(categories);

        var dresses = Assert.Single(categories!, category => category.Slug == "women-clothing-dresses");
        Assert.Equal(CatalogSeedData.WomenClothing, dresses.ParentCategoryId);
        Assert.True(dresses.ChildCount >= 0);
        Assert.True(dresses.ProductCount >= 0);
        Assert.Contains(dresses.Attributes, attribute =>
            attribute.Key == "size"
            && attribute.DataType == "Select"
            && attribute.IsRequired
            && attribute.AllowedValues.Contains("M"));
    }

    [Fact]
    public async Task Admin_CanManageCategoryLifecycleAndAuditActions()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var slug = $"seasonal-{Guid.NewGuid():N}";
        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/categories",
            new UpsertAdminCategoryRequest(null, "Seasonal", slug, 20));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCategoryResponse>();
        Assert.NotNull(created);
        Assert.Equal(slug, created!.Slug);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{created.CategoryId}",
            new UpsertAdminCategoryRequest(null, "Seasonal Edit", $"{slug}-edit", 25));
        updateResponse.EnsureSuccessStatusCode();

        using var deactivateResponse = await client.PostAsync($"/api/admin/categories/{created.CategoryId}/deactivate", null);
        deactivateResponse.EnsureSuccessStatusCode();
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<AdminCategoryResponse>();
        Assert.False(deactivated!.IsActive);

        using var activateResponse = await client.PostAsync($"/api/admin/categories/{created.CategoryId}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var actionTypes = dbContext.AuditLogs
            .Where(log => log.EntityId == created.CategoryId.ToString())
            .Select(log => log.ActionType)
            .ToHashSet();
        Assert.Contains("CategoryCreated", actionTypes);
        Assert.Contains("CategoryUpdated", actionTypes);
        Assert.Contains("CategoryDeactivated", actionTypes);
        Assert.Contains("CategoryActivated", actionTypes);
    }

    [Fact]
    public async Task Buyer_CannotWriteAdminCategories()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/categories",
            new UpsertAdminCategoryRequest(null, "Blocked", $"blocked-{Guid.NewGuid():N}", 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CategoryWrite_RejectsDuplicateSlugAndParentCycle()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);
        var (parentId, childId, parentSlug) = await SeedCategoryTreeAsync(factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/admin/categories",
            new UpsertAdminCategoryRequest(null, "Duplicate", parentSlug, 1));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var cycleResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{parentId}",
            new UpsertAdminCategoryRequest(childId, "Parent", $"{parentSlug}-updated", 1));
        Assert.Equal(HttpStatusCode.BadRequest, cycleResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CanManageCategoryAttributesAndAuditActions()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);
        var categoryId = await SeedCategoryAsync(factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes",
            new UpsertAdminCategoryAttributeRequest("Fit", "fit", "Select", false, ["Slim", "Relaxed"], 1));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var category = await createResponse.Content.ReadFromJsonAsync<AdminCategoryResponse>();
        var attribute = Assert.Single(category!.Attributes, item => item.Key == "fit");

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes/{attribute.AttributeId}",
            new UpsertAdminCategoryAttributeRequest("Fit Type", "fit", "Select", false, ["Slim", "Relaxed", "Oversized"], 2));
        updateResponse.EnsureSuccessStatusCode();

        using var deactivateResponse = await client.PostAsync($"/api/admin/categories/{categoryId}/attributes/{attribute.AttributeId}/deactivate", null);
        deactivateResponse.EnsureSuccessStatusCode();

        using var activateResponse = await client.PostAsync($"/api/admin/categories/{categoryId}/attributes/{attribute.AttributeId}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var actionTypes = dbContext.AuditLogs
            .Where(log => log.EntityId == attribute.AttributeId.ToString())
            .Select(log => log.ActionType)
            .ToHashSet();
        Assert.Contains("CategoryAttributeCreated", actionTypes);
        Assert.Contains("CategoryAttributeUpdated", actionTypes);
        Assert.Contains("CategoryAttributeDeactivated", actionTypes);
        Assert.Contains("CategoryAttributeActivated", actionTypes);
    }

    [Fact]
    public async Task AttributeWrite_ValidatesDataTypeAllowedValuesAndDuplicateKey()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);
        var categoryId = await SeedCategoryAsync(factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var selectWithoutValues = await client.PostAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes",
            new UpsertAdminCategoryAttributeRequest("Size", "size", "Select", false, [], 1));
        Assert.Equal(HttpStatusCode.BadRequest, selectWithoutValues.StatusCode);

        using var textWithValues = await client.PostAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes",
            new UpsertAdminCategoryAttributeRequest("Colour", "colour", "Text", false, ["Black"], 1));
        Assert.Equal(HttpStatusCode.BadRequest, textWithValues.StatusCode);

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes",
            new UpsertAdminCategoryAttributeRequest("Material", "material", "Text", false, null, 1));
        createResponse.EnsureSuccessStatusCode();

        using var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/admin/categories/{categoryId}/attributes",
            new UpsertAdminCategoryAttributeRequest("Material duplicate", "material", "Text", false, null, 2));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task AttributeWrite_GuardsBreakingInUseEdits()
    {
        using var factory = new AdminCategoryTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);
        var seed = await SeedCategoryAttributeProductAsync(factory);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var keyChangeResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{seed.CategoryId}/attributes/{seed.SizeAttributeId}",
            new UpsertAdminCategoryAttributeRequest("Size", "size-code", "Select", false, ["S", "M", "L"], 1));
        Assert.Equal(HttpStatusCode.Conflict, keyChangeResponse.StatusCode);

        using var typeChangeResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{seed.CategoryId}/attributes/{seed.SizeAttributeId}",
            new UpsertAdminCategoryAttributeRequest("Size", "size", "Text", false, null, 1));
        Assert.Equal(HttpStatusCode.Conflict, typeChangeResponse.StatusCode);

        using var removeUsedValueResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{seed.CategoryId}/attributes/{seed.SizeAttributeId}",
            new UpsertAdminCategoryAttributeRequest("Size", "size", "Select", false, ["S", "L"], 1));
        Assert.Equal(HttpStatusCode.Conflict, removeUsedValueResponse.StatusCode);

        using var requireMissingResponse = await client.PutAsJsonAsync(
            $"/api/admin/categories/{seed.CategoryId}/attributes/{seed.MaterialAttributeId}",
            new UpsertAdminCategoryAttributeRequest("Material", "material", "Text", true, null, 2));
        Assert.Equal(HttpStatusCode.Conflict, requireMissingResponse.StatusCode);
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "category-buyer@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminCategoryTestFactory factory,
        HttpClient client)
    {
        const string email = "category-admin@example.test";

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

    private static async Task<Guid> SeedCategoryAsync(AdminCategoryTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var category = new Category(null, "Admin Managed", $"admin-managed-{Guid.NewGuid():N}");
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<(Guid ParentId, Guid ChildId, string ParentSlug)> SeedCategoryTreeAsync(AdminCategoryTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var parentSlug = $"parent-{Guid.NewGuid():N}";
        var parent = new Category(null, "Parent", parentSlug);
        var child = new Category(parent.Id, "Child", $"child-{Guid.NewGuid():N}");
        dbContext.Categories.AddRange(parent, child);
        await dbContext.SaveChangesAsync();
        return (parent.Id, child.Id, parentSlug);
    }

    private static async Task<CategoryAttributeProductSeed> SeedCategoryAttributeProductAsync(AdminCategoryTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var seller = new SellerProfile(Guid.NewGuid());
        var category = new Category(null, "Used Category", $"used-category-{Guid.NewGuid():N}");
        var size = new CategoryAttribute(category.Id, "Size", "size", CategoryAttributeDataType.Select, false, ["S", "M", "L"], 1);
        var material = new CategoryAttribute(category.Id, "Material", "material", CategoryAttributeDataType.Text, false, null, 2);
        var product = new Product(seller.Id);
        product.UpdateDraftDetails(
            category.Id,
            null,
            "Used Dress",
            $"used-dress-{Guid.NewGuid():N}",
            "Short description",
            "Full description for a used product.");
        var sizeValue = new ProductAttributeValue(product.Id, "size", JsonSerializer.Serialize("M"));

        dbContext.AddRange(seller, category, size, material, product, sizeValue);
        await dbContext.SaveChangesAsync();

        return new CategoryAttributeProductSeed(category.Id, size.Id, material.Id);
    }

    private sealed record CategoryAttributeProductSeed(Guid CategoryId, Guid SizeAttributeId, Guid MaterialAttributeId);

    private sealed class AdminCategoryTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAdminCategoryTests_{Guid.NewGuid():N}";

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
