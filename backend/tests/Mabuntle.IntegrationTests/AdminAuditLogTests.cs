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
using Mabuntle.Domain.Admin;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class AdminAuditLogTests
{
    [Fact]
    public async Task Buyer_CannotAccessAuditLogs()
    {
        using var factory = new AdminAuditLogTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanFilterAuditLogs()
    {
        using var factory = new AdminAuditLogTestFactory();
        using var client = factory.CreateClient();
        await SeedAuditLogsAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/audit-logs?entityType=Product&actionType=ProductApproved&pageSize=10");

        response.EnsureSuccessStatusCode();
        var auditLogs = await response.Content.ReadFromJsonAsync<AdminAuditLogSearchResponse>();
        Assert.NotNull(auditLogs);
        Assert.Equal(1, auditLogs!.TotalCount);
        var item = Assert.Single(auditLogs.Items);
        Assert.Equal("ProductApproved", item.ActionType);
        Assert.Equal("Product", item.EntityType);
        Assert.Contains("Published", item.NewValueJson);
    }

    [Fact]
    public async Task Admin_FilterRejectsInvalidDateRange()
    {
        using var factory = new AdminAuditLogTestFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync(
            "/api/admin/audit-logs?fromUtc=2026-05-19T00:00:00Z&toUtc=2026-05-18T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-audit@example.test";
        await RegisterAsync(client, email, MabuntleRoles.Buyer);
        return await LoginAsync(client, email);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", role));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminAuditLogTestFactory factory,
        HttpClient client)
    {
        var email = $"admin-audit-{Guid.NewGuid():N}@example.test";

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

    private static async Task SeedAuditLogsAsync(AdminAuditLogTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        dbContext.AuditLogs.AddRange(
            new AuditLog(
                "admin-user-id",
                MabuntleRoles.Admin,
                "SellerApproved",
                "SellerProfile",
                Guid.NewGuid().ToString(),
                DateTimeOffset.Parse("2026-05-18T10:00:00Z"),
                "{\"verificationStatus\":\"UnderReview\"}",
                "{\"verificationStatus\":\"Verified\"}"),
            new AuditLog(
                "admin-user-id",
                MabuntleRoles.Admin,
                "ProductApproved",
                "Product",
                Guid.NewGuid().ToString(),
                DateTimeOffset.Parse("2026-05-18T11:00:00Z"),
                "{\"status\":\"PendingReview\"}",
                "{\"status\":\"Published\"}",
                "Manual review complete.",
                "127.0.0.1"));

        await dbContext.SaveChangesAsync();
    }

    private sealed class AdminAuditLogTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleAdminAuditLogTests_{Guid.NewGuid():N}";

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
