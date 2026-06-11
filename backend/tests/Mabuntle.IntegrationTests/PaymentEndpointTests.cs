using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mabuntle.Api.Authentication;
using Mabuntle.Api.Payments;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Payments;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Payments;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.IntegrationTests;

public sealed class PaymentEndpointTests
{
    [Fact]
    public async Task BuyerDuplicatePaymentInitiation_ReturnsExistingPayment()
    {
        await using var factory = new PaymentEndpointTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);
        var orderId = await SeedPendingPaymentOrderAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/payments/initiate",
            new InitiatePaymentApiRequest(orderId));
        using var secondResponse = await client.PostAsJsonAsync(
            "/api/payments/initiate",
            new InitiatePaymentApiRequest(orderId));

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.PaymentId, second!.PaymentId);
        Assert.Equal(first.CheckoutUrl, second.CheckoutUrl);
        Assert.Contains($"orderId={orderId}", first.CheckoutUrl?.ToString());
        Assert.StartsWith("fake_", first.ProviderReference, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        Assert.Equal(1, await dbContext.Payments.CountAsync(payment => payment.OrderId == orderId));
    }

    [Fact]
    public async Task PaymentWebhook_RejectsInvalidContentTypeBeforeProcessing()
    {
        await using var factory = new PaymentEndpointTestFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync("/api/payments/webhook/Fake", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PaymentWebhook_RejectsOversizedPayloadBeforeProcessing()
    {
        await using var factory = new PaymentEndpointTestFactory();
        using var client = factory.CreateClient();
        var payload = $$"""
        {
          "eventId": "{{new string('a', 70_000)}}",
          "eventType": "payment.paid",
          "providerReference": "fake_reference",
          "status": "Paid",
          "occurredAtUtc": "2026-05-18T12:01:00Z"
        }
        """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/payments/webhook/Fake", content);

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    [Fact]
    public async Task PayFastPaymentInitiation_ReturnsBridgeCheckoutUrlAndHtml()
    {
        await using var factory = new PaymentEndpointTestFactory(usePayFast: true);
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);
        var orderId = await SeedPendingPaymentOrderAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var initiateResponse = await client.PostAsJsonAsync(
            "/api/payments/initiate",
            new InitiatePaymentApiRequest(orderId));

        initiateResponse.EnsureSuccessStatusCode();
        var payment = await initiateResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.NotNull(payment);
        Assert.Equal(PayFastPaymentProvider.Name, payment!.Provider);
        Assert.NotNull(payment.CheckoutUrl);
        Assert.Contains("/api/payments/payfast/checkout/", payment.CheckoutUrl!.ToString(), StringComparison.Ordinal);

        client.DefaultRequestHeaders.Authorization = null;
        using var checkoutResponse = await client.GetAsync(payment.CheckoutUrl.PathAndQuery);
        checkoutResponse.EnsureSuccessStatusCode();
        var html = await checkoutResponse.Content.ReadAsStringAsync();
        Assert.Equal("text/html", checkoutResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("payfast-checkout", html, StringComparison.Ordinal);
        Assert.Contains(payment.ProviderReference!, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayFastCompleteItn_MarksPaymentPaidAndDuplicateIsIdempotent()
    {
        await using var factory = new PaymentEndpointTestFactory(usePayFast: true);
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);
        var orderId = await SeedPendingPaymentOrderAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var initiateResponse = await client.PostAsJsonAsync(
            "/api/payments/initiate",
            new InitiatePaymentApiRequest(orderId));
        initiateResponse.EnsureSuccessStatusCode();
        var payment = await initiateResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.NotNull(payment);

        client.DefaultRequestHeaders.Authorization = null;
        var payload = BuildPayFastPayload(
            payment!.ProviderReference!,
            "pf_duplicate_1",
            "COMPLETE",
            payment.Amount);
        using var firstContent = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var firstResponse = await client.PostAsync("/api/payments/webhook/payfast", firstContent);
        using var secondContent = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var secondResponse = await client.PostAsync("/api/payments/webhook/payfast", secondContent);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var storedPayment = await dbContext.Payments.SingleAsync(stored => stored.Id == payment.PaymentId);
        var storedOrder = await dbContext.Orders.SingleAsync(order => order.Id == orderId);
        Assert.Equal(PaymentStatus.Paid, storedPayment.Status);
        Assert.Equal(OrderStatus.Paid, storedOrder.Status);
        var storedEvent = await dbContext.PaymentEvents.SingleAsync(paymentEvent => paymentEvent.ProviderEventId == "pf_duplicate_1");
        Assert.Equal(PaymentEventProcessingStatus.Processed, storedEvent.ProcessingStatus);
        Assert.Contains("\"payloadType\":\"form\"", storedEvent.RawPayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"signature\":\"[redacted]\"", storedEvent.RawPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("testing-payfast-passphrase", storedEvent.RawPayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayFastCompleteItn_WithAmountMismatch_DoesNotSettlePayment()
    {
        await using var factory = new PaymentEndpointTestFactory(usePayFast: true);
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);
        var orderId = await SeedPendingPaymentOrderAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var initiateResponse = await client.PostAsJsonAsync(
            "/api/payments/initiate",
            new InitiatePaymentApiRequest(orderId));
        initiateResponse.EnsureSuccessStatusCode();
        var payment = await initiateResponse.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.NotNull(payment);

        client.DefaultRequestHeaders.Authorization = null;
        var payload = BuildPayFastPayload(
            payment!.ProviderReference!,
            "pf_amount_mismatch",
            "COMPLETE",
            1.00m);
        using var content = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await client.PostAsync("/api/payments/webhook/payfast", content);

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var storedPayment = await dbContext.Payments.SingleAsync(stored => stored.Id == payment.PaymentId);
        var storedOrder = await dbContext.Orders.SingleAsync(order => order.Id == orderId);
        var paymentEvent = await dbContext.PaymentEvents.SingleAsync(paymentEvent => paymentEvent.ProviderEventId == "pf_amount_mismatch");
        Assert.Equal(PaymentStatus.Pending, storedPayment.Status);
        Assert.Equal(OrderStatus.PendingPayment, storedOrder.Status);
        Assert.Equal(PaymentEventProcessingStatus.Failed, paymentEvent.ProcessingStatus);
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        var email = $"buyer-payment-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", MabuntleRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!.AccessToken;
    }

    private static async Task<Guid> SeedPendingPaymentOrderAsync(PaymentEndpointTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyer = await dbContext.BuyerProfiles.OrderByDescending(profile => profile.CreatedAtUtc).FirstAsync();
        var seller = new SellerProfile(Guid.NewGuid());
        var product = new Product(seller.Id);
        var variant = new ProductVariant(product.Id, $"SKU-{Guid.NewGuid():N}", "M", "Black", 499m, null, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, seller.Id, "Payment Test Product", variant.Sku, variant.Size, variant.Colour, variant.Price, 1, variant.AvailableQuantity);
        variant.Reserve(1);
        var reservation = new InventoryReservation(variant.Id, buyer.Id, cart.Id, 1, now.AddMinutes(15), now);
        var order = new Order(buyer.Id, seller.Id, cart.Id, now);
        order.AddItem(product.Id, variant.Id, "Payment Test Product", variant.Sku, variant.Size, variant.Colour, variant.Price, 1);

        dbContext.SellerProfiles.Add(seller);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        dbContext.InventoryReservations.Add(reservation);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return order.Id;
    }

    private static string BuildPayFastPayload(
        string providerReference,
        string eventId,
        string status,
        decimal amount)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("m_payment_id", providerReference),
            new("pf_payment_id", eventId),
            new("payment_status", status),
            new("amount_gross", amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
        };
        fields.Add(new(PayFastFormEncoder.SignatureFieldName, PayFastFormEncoder.ComputeSignature(fields, "testing-payfast-passphrase")));

        return PayFastFormEncoder.BuildFormPayload(fields);
    }

    private sealed class PaymentEndpointTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"MabuntlePaymentEndpointTests_{Guid.NewGuid():N}";
        private readonly bool _usePayFast;

        public PaymentEndpointTestFactory(bool usePayFast = false)
        {
            _usePayFast = usePayFast;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            if (_usePayFast)
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PaymentProvider:ProviderName"] = PayFastPaymentProvider.Name,
                        ["PayFast:MerchantId"] = "100001",
                        ["PayFast:MerchantKey"] = "merchant-key",
                        ["PayFast:Passphrase"] = "testing-payfast-passphrase",
                        ["PayFast:ProcessUrl"] = "https://sandbox.payfast.co.za/eng/process",
                        ["PayFast:ValidateUrl"] = "https://sandbox.payfast.co.za/eng/query/validate",
                        ["PayFast:NotifyUrl"] = "https://localhost:7268/api/payments/webhook/payfast",
                        ["PayFast:CheckoutBridgeBaseUrl"] = "https://localhost:7268",
                        ["PayFast:RequireRemoteValidation"] = "false"
                    });
                });
            }

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
