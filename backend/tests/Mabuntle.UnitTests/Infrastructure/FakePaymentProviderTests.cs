using Microsoft.Extensions.Options;
using Mabuntle.Application.Payments;
using Mabuntle.Infrastructure.Payments;

namespace Mabuntle.UnitTests.Infrastructure;

public class FakePaymentProviderTests
{
    [Fact]
    public async Task InitializePaymentAsync_CanSimulateFailureFromMetadata()
    {
        var provider = CreateProvider(new PaymentProviderOptions());

        var result = await provider.InitializePaymentAsync(CreateRequest(new Dictionary<string, string>
        {
            [FakePaymentProvider.MetadataOutcomeKey] = FakePaymentOutcomes.Failure
        }));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.FakeProviderFailed", result.Error.Code);
    }

    [Fact]
    public async Task VerifyPaymentAsync_ReturnsPaidForNormalReference()
    {
        var provider = CreateProvider(new PaymentProviderOptions());

        var result = await provider.VerifyPaymentAsync(new PaymentVerificationRequest("fake_reference"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Paid", result.Value.Status);
        Assert.Equal("Fake", result.Value.Provider);
    }

    [Fact]
    public async Task ParseWebhookAsync_ReturnsStructuredEvent()
    {
        var provider = CreateProvider(new PaymentProviderOptions());
        const string payload = """
            {
              "eventId": "evt_1",
              "eventType": "payment.paid",
              "providerReference": "fake_reference",
              "status": "Paid",
              "occurredAtUtc": "2026-05-18T12:00:00Z"
            }
            """;

        var result = await provider.ParseWebhookAsync(new PaymentWebhookParseRequest(
            payload,
            new Dictionary<string, string>()));

        Assert.True(result.IsSuccess);
        Assert.Equal("evt_1", result.Value.EventId);
        Assert.Equal("payment.paid", result.Value.EventType);
        Assert.Equal("fake_reference", result.Value.ProviderReference);
        Assert.Equal("Paid", result.Value.Status);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_ReturnsUnauthorizedWhenSignatureDoesNotMatch()
    {
        var provider = CreateProvider(new PaymentProviderOptions
        {
            WebhookSigningSecret = "test-secret"
        });

        var result = await provider.VerifyWebhookSignatureAsync(new PaymentWebhookSignatureVerificationRequest(
            "{\"eventId\":\"evt_1\"}",
            new Dictionary<string, string>
            {
                [FakePaymentProvider.HeaderSignatureKey] = "invalid"
            }));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.InvalidWebhookSignature", result.Error.Code);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_ReturnsUnauthorizedWhenSecretIsMissing()
    {
        var provider = CreateProvider(new PaymentProviderOptions());

        var result = await provider.VerifyWebhookSignatureAsync(new PaymentWebhookSignatureVerificationRequest(
            "{\"eventId\":\"evt_1\"}",
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.WebhookSigningSecretNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task RefundPaymentAsync_UsesIdempotencyKeyForDeterministicProviderReference()
    {
        var provider = CreateProvider(new PaymentProviderOptions());
        var request = new PaymentRefundRequest(
            "fake_reference",
            100m,
            "ZAR",
            "Approved refund.",
            "refund-idempotency-key",
            new Dictionary<string, string>());

        var first = await provider.RefundPaymentAsync(request);
        var second = await provider.RefundPaymentAsync(request);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ProviderRefundReference, second.Value.ProviderRefundReference);
        Assert.Equal("fake_refund_refund-idempotency-key", first.Value.ProviderRefundReference);
    }

    private static PaymentInitiationRequest CreateRequest(IReadOnlyDictionary<string, string> metadata) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "ZAR",
            "Mabuntle test payment",
            new Uri("http://localhost:4200/checkout/success"),
            new Uri("http://localhost:4200/checkout/failed"),
            metadata);

    private static FakePaymentProvider CreateProvider(PaymentProviderOptions options) =>
        new(Options.Create(options), TimeProvider.System);
}
