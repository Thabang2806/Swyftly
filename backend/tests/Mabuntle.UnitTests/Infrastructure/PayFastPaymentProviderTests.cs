using System.Net;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Payments;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Infrastructure.Payments;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class PayFastPaymentProviderTests
{
    [Fact]
    public async Task InitializePaymentAsync_ReturnsBridgeCheckoutUrlAndUsesLocalPaymentIdReference()
    {
        var paymentId = Guid.NewGuid();
        var provider = CreateProvider(new PayFastOptions
        {
            MerchantId = "merchant-id",
            MerchantKey = "merchant-key",
            Passphrase = "passphrase",
            CheckoutBridgeBaseUrl = "https://localhost:7268"
        });

        var result = await provider.InitializePaymentAsync(new PaymentInitiationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            199.99m,
            "ZAR",
            "Mabuntle order",
            new Uri("http://localhost:4200/checkout/success"),
            new Uri("http://localhost:4200/checkout/failed"),
            new Dictionary<string, string>
            {
                ["paymentId"] = paymentId.ToString()
            }));

        Assert.True(result.IsSuccess);
        Assert.Equal(paymentId.ToString("N"), result.Value.ProviderReference);
        Assert.Equal(
            $"https://localhost:7268/api/payments/payfast/checkout/{paymentId:N}",
            result.Value.CheckoutUrl?.ToString());
    }

    [Fact]
    public async Task ParseWebhookAsync_MapsCompleteItnToPaidEventWithAmountAndCurrency()
    {
        var provider = CreateProvider(new PayFastOptions());
        var payload = "m_payment_id=pay_123&pf_payment_id=pf_456&payment_status=COMPLETE&amount_gross=149.95";

        var result = await provider.ParseWebhookAsync(new PaymentWebhookParseRequest(
            payload,
            new Dictionary<string, string>()));

        Assert.True(result.IsSuccess);
        Assert.Equal("pf_456", result.Value.EventId);
        Assert.Equal("pay_123", result.Value.ProviderReference);
        Assert.Equal("Paid", result.Value.Status);
        Assert.Equal(149.95m, result.Value.Amount);
        Assert.Equal("ZAR", result.Value.Currency);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_RejectsInvalidSignature()
    {
        var provider = CreateProvider(new PayFastOptions
        {
            Passphrase = "secret-passphrase",
            RequireRemoteValidation = false
        });

        var result = await provider.VerifyWebhookSignatureAsync(new PaymentWebhookSignatureVerificationRequest(
            "m_payment_id=pay_123&pf_payment_id=pf_456&payment_status=COMPLETE&signature=bad",
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.InvalidPayFastSignature", result.Error.Code);
    }

    [Fact]
    public async Task VerifyWebhookSignatureAsync_RequiresRemoteValidResponseWhenEnabled()
    {
        var options = new PayFastOptions
        {
            Passphrase = "secret-passphrase",
            ValidateUrl = "https://sandbox.payfast.co.za/eng/query/validate",
            RequireRemoteValidation = true
        };
        var fields = new List<KeyValuePair<string, string>>
        {
            new("m_payment_id", "pay_123"),
            new("pf_payment_id", "pf_456"),
            new("payment_status", "COMPLETE")
        };
        fields.Add(new(PayFastFormEncoder.SignatureFieldName, PayFastFormEncoder.ComputeSignature(fields, options.Passphrase)));
        var provider = CreateProvider(options, new StaticHttpMessageHandler("INVALID"));

        var result = await provider.VerifyWebhookSignatureAsync(new PaymentWebhookSignatureVerificationRequest(
            PayFastFormEncoder.BuildFormPayload(fields),
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.PayFastRemoteValidationFailed", result.Error.Code);
    }

    [Fact]
    public async Task RefundPaymentAsync_ReturnsManualProviderActionRequired()
    {
        var provider = CreateProvider(new PayFastOptions());

        var result = await provider.RefundPaymentAsync(new PaymentRefundRequest(
            "payfast_reference",
            100m,
            "ZAR",
            "Approved refund",
            Guid.NewGuid().ToString("N"),
            new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal(PayFastPaymentProvider.ManualRefundRequiredCode, result.Error.Code);
    }

    [Fact]
    public void PayFastCheckoutFormBuilder_BuildsSignedAutoSubmitHtml()
    {
        var now = DateTimeOffset.UtcNow;
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Dress", "SKU-1", "M", "Black", 299.99m, 1);

        var payment = new Payment(
            order.Id,
            order.BuyerId,
            PayFastPaymentProvider.Name,
            order.TotalAmount,
            "ZAR",
            now);
        payment.SetProviderReference(payment.Id.ToString("N"), now);

        var builder = new PayFastCheckoutFormBuilder(
            Options.Create(new PayFastOptions
            {
                MerchantId = "merchant-id",
                MerchantKey = "merchant-key",
                Passphrase = "secret-passphrase",
                ProcessUrl = "https://sandbox.payfast.co.za/eng/process",
                NotifyUrl = "https://localhost:7268/api/payments/webhook/payfast"
            }),
            Options.Create(new PaymentProviderOptions
            {
                SuccessRedirectUrl = "http://localhost:4200/checkout/success",
                FailureRedirectUrl = "http://localhost:4200/checkout/failed"
            }));

        var result = builder.Build(payment, order);

        Assert.True(result.IsSuccess);
        Assert.Contains("method=\"post\"", result.Value.Html, StringComparison.Ordinal);
        Assert.Contains("document.getElementById('payfast-checkout').submit()", result.Value.Html, StringComparison.Ordinal);
        Assert.Contains(result.Value.Fields, field => field.Key == "m_payment_id" && field.Value == payment.ProviderReference);
        Assert.Contains(result.Value.Fields, field => field.Key == "signature" && !string.IsNullOrWhiteSpace(field.Value));
    }

    private static PayFastPaymentProvider CreateProvider(
        PayFastOptions options,
        HttpMessageHandler? handler = null)
    {
        var httpClient = handler is null
            ? new HttpClient(new StaticHttpMessageHandler("VALID"))
            : new HttpClient(handler);

        return new PayFastPaymentProvider(Options.Create(options), httpClient, TimeProvider.System);
    }

    private sealed class StaticHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }
}
