using Microsoft.Extensions.Options;
using Mabuntle.Application.Payments;
using Mabuntle.Infrastructure.Payments;

namespace Mabuntle.UnitTests.Application;

public class PaymentInitiationServiceTests
{
    [Fact]
    public async Task InitiateAsync_UsesPaymentProviderAndReturnsCheckoutSession()
    {
        var provider = CreateProvider(new PaymentProviderOptions());
        var service = new PaymentInitiationService(provider);
        var orderId = Guid.NewGuid();

        var result = await service.InitiateAsync(CreateRequest(orderId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Fake", result.Value.Provider);
        Assert.StartsWith($"fake_{orderId:N}_", result.Value.ProviderReference, StringComparison.Ordinal);
        Assert.NotNull(result.Value.CheckoutUrl);
        Assert.Equal("Initialized", result.Value.Status);
    }

    [Fact]
    public async Task InitiateAsync_ReturnsValidationFailureBeforeProviderCall()
    {
        var provider = CreateProvider(new PaymentProviderOptions());
        var service = new PaymentInitiationService(provider);

        var result = await service.InitiateAsync(CreateRequest(Guid.Empty) with { Amount = 0 });

        Assert.True(result.IsFailure);
        Assert.Contains("orderId", result.Error.Details!.Keys);
        Assert.Contains("amount", result.Error.Details.Keys);
    }

    [Fact]
    public async Task InitiateAsync_ReturnsProviderFailureWhenFakeProviderIsConfiguredToFail()
    {
        var provider = CreateProvider(new PaymentProviderOptions
        {
            FakeOutcome = FakePaymentOutcomes.Failure
        });
        var service = new PaymentInitiationService(provider);

        var result = await service.InitiateAsync(CreateRequest(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.FakeProviderFailed", result.Error.Code);
    }

    private static PaymentInitiationRequest CreateRequest(Guid orderId) =>
        new(
            orderId,
            Guid.NewGuid(),
            998m,
            "ZAR",
            "Mabuntle order payment",
            new Uri("http://localhost:4200/checkout/success"),
            new Uri("http://localhost:4200/checkout/failed"),
            new Dictionary<string, string>());

    private static FakePaymentProvider CreateProvider(PaymentProviderOptions options) =>
        new(Options.Create(options), TimeProvider.System);
}
