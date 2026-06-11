using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Mabuntle.Api.Payments;
using Mabuntle.Application.Payments;
using Mabuntle.Infrastructure.Payments;

namespace Mabuntle.IntegrationTests;

public sealed class PaymentProviderHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyForFakeProvider()
    {
        var healthCheck = new PaymentProviderHealthCheck(
            Options.Create(new PaymentProviderOptions { ProviderName = FakePaymentProvider.Name }),
            Options.Create(new PayFastOptions()));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("Fake", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyForDisabledProvider()
    {
        var healthCheck = new PaymentProviderHealthCheck(
            Options.Create(new PaymentProviderOptions { ProviderName = DisabledPaymentProvider.Name }),
            Options.Create(new PayFastOptions()));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("disabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyForIncompletePayFastConfiguration()
    {
        var healthCheck = new PaymentProviderHealthCheck(
            Options.Create(new PaymentProviderOptions { ProviderName = PayFastPaymentProvider.Name }),
            Options.Create(new PayFastOptions
            {
                MerchantId = "",
                MerchantKey = "",
                ProcessUrl = "https://sandbox.payfast.co.za/eng/process",
                NotifyUrl = "not-a-url",
                CheckoutBridgeBaseUrl = "https://localhost:7268",
                ValidateUrl = "",
                RequireRemoteValidation = true
            }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("incomplete", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PayFast:MerchantId", result.Data.Keys);
        Assert.Contains("PayFast:MerchantKey", result.Data.Keys);
        Assert.Contains("PayFast:NotifyUrl", result.Data.Keys);
        Assert.Contains("PayFast:ValidateUrl", result.Data.Keys);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyForConfiguredPayFastProvider()
    {
        var healthCheck = new PaymentProviderHealthCheck(
            Options.Create(new PaymentProviderOptions { ProviderName = PayFastPaymentProvider.Name }),
            Options.Create(new PayFastOptions
            {
                MerchantId = "100001",
                MerchantKey = "merchant-key",
                ProcessUrl = "https://sandbox.payfast.co.za/eng/process",
                NotifyUrl = "https://api.mabuntle.example/api/payments/webhook/payfast",
                CheckoutBridgeBaseUrl = "https://api.mabuntle.example",
                ValidateUrl = "https://sandbox.payfast.co.za/eng/query/validate",
                RequireRemoteValidation = true
            }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("PayFast", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyForUnsupportedProvider()
    {
        var healthCheck = new PaymentProviderHealthCheck(
            Options.Create(new PaymentProviderOptions { ProviderName = "UnknownProvider" }),
            Options.Create(new PayFastOptions()));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not supported", result.Description, StringComparison.Ordinal);
    }
}
