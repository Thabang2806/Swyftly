using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task SearchEndpoint_ReturnsTooManyRequestsWhenPolicyLimitIsExceeded()
    {
        using var factory = new RateLimitingTestFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage? rejectedResponse = null;
        for (var index = 0; index < 121; index++)
        {
            var response = await client.GetAsync("/api/products/search?query=dress");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejectedResponse = response;
                break;
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        Assert.NotNull(rejectedResponse);
        var body = await rejectedResponse!.Content.ReadAsStringAsync();
        Assert.Contains("RateLimit.Exceeded", body);
        rejectedResponse.Dispose();
    }

    [Fact]
    public async Task AdImpressionEndpoint_ReturnsTooManyRequestsWhenPolicyLimitIsExceeded()
    {
        using var factory = new RateLimitingTestFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage? rejectedResponse = null;
        for (var index = 0; index < 121; index++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/ads/impressions",
                new
                {
                    AdCampaignId = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Placement = "shop-grid",
                    AnonymousVisitorId = (string?)null
                });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejectedResponse = response;
                break;
            }

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            response.Dispose();
        }

        Assert.NotNull(rejectedResponse);
        var body = await rejectedResponse!.Content.ReadAsStringAsync();
        Assert.Contains("RateLimit.Exceeded", body);
        rejectedResponse.Dispose();
    }

    private sealed class RateLimitingTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntleRateLimitingTests_{Guid.NewGuid():N}";

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
