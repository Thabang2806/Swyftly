using Swyftly.Application.Payments;
using Swyftly.Infrastructure.Payments;

namespace Swyftly.UnitTests.Infrastructure;

public sealed class DisabledPaymentProviderTests
{
    [Fact]
    public async Task InitializePaymentAsync_ReturnsDisabledFailure()
    {
        var provider = new DisabledPaymentProvider();

        var result = await provider.InitializePaymentAsync(new PaymentInitiationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "ZAR",
            "Swyftly order",
            new Uri("https://swyftly.co.za/checkout/success"),
            new Uri("https://swyftly.co.za/checkout/failed"),
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.ProviderDisabled", result.Error.Code);
        Assert.Equal(DisabledPaymentProvider.Name, provider.ProviderName);
    }

    [Fact]
    public async Task RefundPaymentAsync_ReturnsDisabledFailure()
    {
        var provider = new DisabledPaymentProvider();

        var result = await provider.RefundPaymentAsync(new PaymentRefundRequest(
            "provider-reference",
            100m,
            "ZAR",
            "Refund request",
            Guid.NewGuid().ToString("N"),
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.ProviderDisabled", result.Error.Code);
    }
}
