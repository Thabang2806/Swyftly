using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Swyftly.Api.Notifications;
using Swyftly.Infrastructure.Notifications;

namespace Swyftly.IntegrationTests;

public sealed class EmailDeliveryHealthCheckTests
{
    [Fact]
    public async Task LogOnlyProvider_IsHealthyOutsideProduction()
    {
        var healthCheck = new EmailDeliveryHealthCheck(
            Options.Create(new EmailDeliveryOptions { ProviderName = LogOnlyEmailDeliveryProvider.Name }),
            new TestEnvironment("Development"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task LogOnlyProvider_IsUnhealthyInProduction()
    {
        var healthCheck = new EmailDeliveryHealthCheck(
            Options.Create(new EmailDeliveryOptions { ProviderName = LogOnlyEmailDeliveryProvider.Name }),
            new TestEnvironment("Production"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("EmailDelivery:ProviderName", result.Data.Keys);
    }

    [Fact]
    public async Task SmtpProvider_RequiresFromAddressAndHost()
    {
        var healthCheck = new EmailDeliveryHealthCheck(
            Options.Create(new EmailDeliveryOptions { ProviderName = SmtpEmailDeliveryProvider.Name }),
            new TestEnvironment("Development"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("EmailDelivery:FromAddress", result.Data.Keys);
        Assert.Contains("EmailDelivery:Smtp:Host", result.Data.Keys);
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Swyftly.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
