using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Swyftly.Application.Analytics;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Ledger;
using Swyftly.Application.Payments;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Infrastructure.Advertising;
using Swyftly.Infrastructure.Ledger;
using Swyftly.Infrastructure.Payments;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.UnitTests.Infrastructure;

public class PaymentServiceTests
{
    [Fact]
    public async Task InitiatePaymentAsync_CreatesPendingPaymentAndStoresProviderReference()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var service = CreateService(dbContext, new PaymentProviderOptions());

        var result = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.StartsWith("fake_", result.Value.ProviderReference, StringComparison.Ordinal);
        var payment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(seed.Order.TotalAmount, payment.Amount);
        Assert.Equal(result.Value.ProviderReference, payment.ProviderReference);
        Assert.NotNull(result.Value.CheckoutUrl);
        Assert.Equal(result.Value.CheckoutUrl?.ToString(), payment.CheckoutUrl);
    }

    [Fact]
    public async Task InitiatePaymentAsync_ReturnsExistingActivePaymentWithoutCallingProviderAgain()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var provider = new CountingPaymentProvider(SignedWebhookOptions());
        var service = CreateService(dbContext, SignedWebhookOptions(), provider);

        var first = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));
        var second = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.PaymentId, second.Value.PaymentId);
        Assert.Equal(first.Value.CheckoutUrl, second.Value.CheckoutUrl);
        Assert.Equal(1, provider.InitializePaymentCallCount);
        Assert.Equal(1, await dbContext.Payments.CountAsync());
    }

    [Fact]
    public async Task InitiatePaymentAsync_MarksPaymentFailedWhenProviderFails()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var service = CreateService(dbContext, new PaymentProviderOptions
        {
            FakeOutcome = FakePaymentOutcomes.Failure
        });

        var result = await service.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(result.IsFailure);
        var payment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Null(payment.ProviderReference);
    }

    [Fact]
    public async Task InitiatePaymentAsync_AllowsRetryAfterFailedPayment()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var failed = await CreateService(dbContext, new PaymentProviderOptions
        {
            FakeOutcome = FakePaymentOutcomes.Failure
        }).InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));
        var retryProvider = new CountingPaymentProvider(SignedWebhookOptions());
        var retryService = CreateService(dbContext, SignedWebhookOptions(), retryProvider);

        var retry = await retryService.InitiatePaymentAsync(new InitiatePaymentRequest(seed.Buyer.Id, seed.Order.Id));

        Assert.True(failed.IsFailure);
        Assert.True(retry.IsSuccess);
        Assert.Equal(1, retryProvider.InitializePaymentCallCount);
        Assert.Equal(2, await dbContext.Payments.CountAsync());
        Assert.Equal(1, await dbContext.Payments.CountAsync(payment => payment.Status == PaymentStatus.Failed));
        Assert.Equal(1, await dbContext.Payments.CountAsync(payment => payment.Status == PaymentStatus.Pending));
    }

    [Fact]
    public async Task ProcessWebhookAsync_SuccessMarksPaymentOrderReservationsAndLedgerOnce()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = PaidPayload("evt_1", "fake_reference");
        var webhook = new ProcessPaymentWebhookRequest("Fake", payload, SignedHeaders(payload, options.WebhookSigningSecret));

        var first = await service.ProcessWebhookAsync(webhook);
        var duplicate = await service.ProcessWebhookAsync(webhook);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Paid, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Equal(InventoryReservationStatus.Confirmed, (await dbContext.InventoryReservations.SingleAsync()).Status);
        Assert.Empty(await dbContext.CartItems.ToListAsync());
        Assert.Null((await dbContext.Carts.SingleAsync()).SellerId);
        Assert.Equal(4, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == payment.Id));
        Assert.Equal(1, await dbContext.PaymentEvents.CountAsync());
        Assert.Equal(PaymentEventProcessingStatus.Processed, (await dbContext.PaymentEvents.SingleAsync()).ProcessingStatus);
        var movement = await dbContext.InventoryMovements.SingleAsync(movement => movement.MovementType == InventoryMovementType.ReservationConfirmed);
        Assert.Equal(payment.Id, movement.PaymentId);
        Assert.Equal(seed.Order.Id, movement.OrderId);
        Assert.Equal(2, movement.ReservedQuantityBefore);
        Assert.Equal(2, movement.ReservedQuantityAfter);
    }

    [Fact]
    public async Task ProcessWebhookAsync_AuthorizedDoesNotSettleOrderReservationOrLedger()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_authorized_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = WebhookPayload("evt_authorized", "fake_authorized_reference", "payment.authorized", "Authorized");

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            payload,
            SignedHeaders(payload, options.WebhookSigningSecret)));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Authorized, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.PendingPayment, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Equal(InventoryReservationStatus.Active, (await dbContext.InventoryReservations.SingleAsync()).Status);
        Assert.Single(await dbContext.CartItems.ToListAsync());
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
        Assert.Equal(PaymentEventProcessingStatus.Processed, (await dbContext.PaymentEvents.SingleAsync()).ProcessingStatus);
    }

    [Fact]
    public async Task ProcessWebhookAsync_FailedThenPaidDoesNotResurrectCancelledOrder()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_late_paid_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var failedPayload = FailedPayload("evt_failed_first", "fake_late_paid_reference");
        var paidPayload = PaidPayload("evt_paid_late", "fake_late_paid_reference");

        var failed = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            failedPayload,
            SignedHeaders(failedPayload, options.WebhookSigningSecret)));
        var latePaid = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            paidPayload,
            SignedHeaders(paidPayload, options.WebhookSigningSecret)));

        Assert.True(failed.IsSuccess);
        Assert.True(latePaid.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Cancelled, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
        Assert.Equal(0, (await dbContext.ProductVariants.SingleAsync()).ReservedQuantity);
        Assert.Equal(2, await dbContext.PaymentEvents.CountAsync());
        Assert.Equal(1, await dbContext.PaymentEvents.CountAsync(paymentEvent => paymentEvent.ProcessingStatus == PaymentEventProcessingStatus.Failed));
    }

    [Fact]
    public async Task ProcessWebhookAsync_FailureReleasesReservationAndCancelsOrder()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_failed_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = FailedPayload("evt_2", "fake_failed_reference");

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            payload,
            SignedHeaders(payload, options.WebhookSigningSecret)));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, (await dbContext.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Cancelled, (await dbContext.Orders.SingleAsync()).Status);
        Assert.Equal(InventoryReservationStatus.Cancelled, (await dbContext.InventoryReservations.SingleAsync()).Status);
        Assert.Equal(0, (await dbContext.ProductVariants.SingleAsync()).ReservedQuantity);
        Assert.Single(await dbContext.CartItems.ToListAsync());
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
        var movement = await dbContext.InventoryMovements.SingleAsync(movement => movement.MovementType == InventoryMovementType.PaymentFailedReservationReleased);
        Assert.Equal(payment.Id, movement.PaymentId);
        Assert.Equal(seed.Order.Id, movement.OrderId);
        Assert.Equal(2, movement.ReservedQuantityBefore);
        Assert.Equal(0, movement.ReservedQuantityAfter);
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidSignatureDoesNotPersistPaymentEvent()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var service = CreateService(dbContext, SignedWebhookOptions());

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            PaidPayload("evt_invalid", "fake_reference"),
            new Dictionary<string, string>
            {
                [FakePaymentProvider.HeaderSignatureKey] = "invalid"
            }));

        Assert.True(result.IsFailure);
        Assert.Equal("Payments.InvalidWebhookSignature", result.Error.Code);
        Assert.Empty(await dbContext.PaymentEvents.ToListAsync());
        Assert.Equal(PaymentStatus.Pending, (await dbContext.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PersistsSanitizedJsonPayload()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "Fake", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("fake_sensitive_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var options = SignedWebhookOptions();
        var service = CreateService(dbContext, options);
        var payload = """
        {
          "eventId": "evt_sensitive",
          "eventType": "payment.paid",
          "providerReference": "fake_sensitive_reference",
          "status": "Paid",
          "signature": "do-not-store",
          "nested": { "accessToken": "do-not-store-token" },
          "occurredAtUtc": "2026-05-18T12:01:00Z"
        }
        """;

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "Fake",
            payload,
            SignedHeaders(payload, options.WebhookSigningSecret)));

        Assert.True(result.IsSuccess);
        var storedPayload = (await dbContext.PaymentEvents.SingleAsync()).RawPayloadJson;
        Assert.Contains("\"payloadType\":\"json\"", storedPayload, StringComparison.Ordinal);
        Assert.Contains("\"signature\":\"[redacted]\"", storedPayload, StringComparison.Ordinal);
        Assert.Contains("\"accessToken\":\"[redacted]\"", storedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-store", storedPayload, StringComparison.Ordinal);
        using var document = System.Text.Json.JsonDocument.Parse(storedPayload);
        Assert.Equal("Fake", document.RootElement.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task ProcessWebhookAsync_PersistsSanitizedFormPayload()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedOrderWithReservationAsync(dbContext);
        var provider = new StaticWebhookPaymentProvider(new PaymentWebhookEvent(
            "PayFast",
            "pf_sensitive",
            "payfast.complete",
            "payfast_reference",
            "Paid",
            DateTimeOffset.Parse("2026-05-18T12:01:00Z"),
            "m_payment_id=payfast_reference&pf_payment_id=pf_sensitive&payment_status=COMPLETE&signature=do-not-store",
            seed.Order.TotalAmount,
            "ZAR"));
        var payment = new Payment(seed.Order.Id, seed.Buyer.Id, "PayFast", seed.Order.TotalAmount, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payment.SetProviderReference("payfast_reference", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var service = CreateService(dbContext, SignedWebhookOptions(), provider);

        var result = await service.ProcessWebhookAsync(new ProcessPaymentWebhookRequest(
            "PayFast",
            provider.Event.Payload,
            new Dictionary<string, string>()));

        Assert.True(result.IsSuccess);
        var storedPayload = (await dbContext.PaymentEvents.SingleAsync()).RawPayloadJson;
        Assert.Contains("\"payloadType\":\"form\"", storedPayload, StringComparison.Ordinal);
        Assert.Contains("\"signature\":\"[redacted]\"", storedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-store", storedPayload, StringComparison.Ordinal);
        using var document = System.Text.Json.JsonDocument.Parse(storedPayload);
        Assert.Equal("payfast_reference", document.RootElement.GetProperty("fields").GetProperty("m_payment_id").GetString());
    }

    [Fact]
    public async Task WebhookPayloadRetention_RedactsOnlyExpiredUnredactedPayloads()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.Parse("2026-05-19T12:00:00Z");
        var expiredEvent = new PaymentEvent(
            null,
            "Fake",
            "evt_expired",
            "payment.paid",
            """{"payloadType":"json","body":{"signature":"redacted-but-still-raw"}}""",
            now.AddDays(-91));
        var recentEvent = new PaymentEvent(
            null,
            "Fake",
            "evt_recent",
            "payment.paid",
            """{"payloadType":"json","body":{"status":"Paid"}}""",
            now.AddDays(-10));
        var alreadyRedactedEvent = new PaymentEvent(
            null,
            "Fake",
            "evt_already_redacted",
            "payment.paid",
            """{"payloadType":"json","body":{"status":"Paid"}}""",
            now.AddDays(-120));
        alreadyRedactedEvent.RedactRawPayload("""{"payloadType":"redacted","redacted":true}""", now.AddDays(-1));
        dbContext.PaymentEvents.AddRange(expiredEvent, recentEvent, alreadyRedactedEvent);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var retentionService = new EfPaymentWebhookPayloadRetentionService(
            dbContext,
            Options.Create(new PaymentWebhookPayloadRetentionOptions
            {
                RetentionDays = 90,
                BatchSize = 100
            }));

        var result = await retentionService.RedactExpiredPayloadsAsync(now);
        var secondRun = await retentionService.RedactExpiredPayloadsAsync(now);

        Assert.Equal(1, result.RedactedCount);
        Assert.Equal(now.AddDays(-90), result.CutoffUtc);
        Assert.Equal(0, secondRun.RedactedCount);

        var storedExpiredEvent = await dbContext.PaymentEvents.SingleAsync(paymentEvent => paymentEvent.ProviderEventId == "evt_expired");
        var storedRecentEvent = await dbContext.PaymentEvents.SingleAsync(paymentEvent => paymentEvent.ProviderEventId == "evt_recent");
        var storedAlreadyRedactedEvent = await dbContext.PaymentEvents.SingleAsync(paymentEvent => paymentEvent.ProviderEventId == "evt_already_redacted");

        Assert.Equal(now, storedExpiredEvent.RawPayloadRedactedAtUtc);
        Assert.Contains("\"payloadType\":\"redacted\"", storedExpiredEvent.RawPayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"retention_expired\"", storedExpiredEvent.RawPayloadJson, StringComparison.Ordinal);
        Assert.Null(storedRecentEvent.RawPayloadRedactedAtUtc);
        Assert.NotNull(storedAlreadyRedactedEvent.RawPayloadRedactedAtUtc);
    }

    private static EfPaymentService CreateService(SwyftlyDbContext dbContext, PaymentProviderOptions paymentOptions)
    {
        var provider = new FakePaymentProvider(Options.Create(paymentOptions), TimeProvider.System);
        return CreateService(dbContext, paymentOptions, provider);
    }

    private static EfPaymentService CreateService(
        SwyftlyDbContext dbContext,
        PaymentProviderOptions paymentOptions,
        IPaymentProvider provider)
    {
        var ledger = new EfLedgerService(dbContext, Options.Create(new LedgerOptions()));
        var adTracking = new EfAdTrackingService(dbContext, TimeProvider.System);
        return new EfPaymentService(
            dbContext,
            provider,
            ledger,
            adTracking,
            new NoOpStorefrontAnalyticsService(),
            Options.Create(paymentOptions),
            TimeProvider.System);
    }

    private static PaymentProviderOptions SignedWebhookOptions() =>
        new()
        {
            WebhookSigningSecret = "test-webhook-secret"
        };

    private static Dictionary<string, string> SignedHeaders(string payload, string secret) =>
        new()
        {
            [FakePaymentProvider.HeaderSignatureKey] = ComputeSignature(payload, secret)
        };

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class NoOpStorefrontAnalyticsService : IStorefrontAnalyticsService
    {
        public Task<Result<StorefrontFunnelEventResult>> RecordClientEventAsync(
            StorefrontFunnelEventRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<StorefrontFunnelEventResult>.Success(new StorefrontFunnelEventResult(false, null, "Skipped")));

        public Task RecordOrderCreatedAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static async Task<(BuyerProfile Buyer, ProductVariant Variant, Order Order)> SeedOrderWithReservationAsync(SwyftlyDbContext dbContext)
    {
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyer = new BuyerProfile(Guid.NewGuid());
        var product = new Product(Guid.NewGuid());
        var variant = new ProductVariant(product.Id, "SKU-1", "M", "Black", 499m, 599m, 5);
        var cart = new Cart(buyer.Id);
        cart.AddOrUpdateItem(product.Id, variant.Id, product.SellerId, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2, variant.AvailableQuantity);
        variant.Reserve(2);
        var reservation = new InventoryReservation(variant.Id, buyer.Id, cart.Id, 2, now.AddMinutes(15), now);
        var order = new Order(buyer.Id, product.SellerId, cart.Id, now);
        order.AddItem(product.Id, variant.Id, "Cotton Dress", variant.Sku, variant.Size, variant.Colour, variant.Price, 2);

        dbContext.BuyerProfiles.Add(buyer);
        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Carts.Add(cart);
        dbContext.InventoryReservations.Add(reservation);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return (buyer, variant, order);
    }

    private static string PaidPayload(string eventId, string providerReference) =>
        WebhookPayload(eventId, providerReference, "payment.paid", "Paid");

    private static string FailedPayload(string eventId, string providerReference) =>
        WebhookPayload(eventId, providerReference, "payment.failed", "Failed");

    private static string WebhookPayload(
        string eventId,
        string providerReference,
        string eventType,
        string status) =>
        $$"""
        {
          "eventId": "{{eventId}}",
          "eventType": "{{eventType}}",
          "providerReference": "{{providerReference}}",
          "status": "{{status}}",
          "occurredAtUtc": "2026-05-18T12:01:00Z"
        }
        """;

    private static SwyftlyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"PaymentServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SwyftlyDbContext(options);
    }

    private sealed class CountingPaymentProvider(PaymentProviderOptions options) : IPaymentProvider
    {
        private readonly FakePaymentProvider _inner = new(Options.Create(options), TimeProvider.System);

        public int InitializePaymentCallCount { get; private set; }

        public string ProviderName => _inner.ProviderName;

        public async Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
            PaymentInitiationRequest request,
            CancellationToken cancellationToken = default)
        {
            InitializePaymentCallCount++;
            return await _inner.InitializePaymentAsync(request, cancellationToken);
        }

        public Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
            PaymentVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.VerifyPaymentAsync(request, cancellationToken);

        public Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
            PaymentWebhookParseRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.ParseWebhookAsync(request, cancellationToken);

        public Task<Result<PaymentRefundResult>> RefundPaymentAsync(
            PaymentRefundRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.RefundPaymentAsync(request, cancellationToken);

        public Task<Result> VerifyWebhookSignatureAsync(
            PaymentWebhookSignatureVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            _inner.VerifyWebhookSignatureAsync(request, cancellationToken);
    }

    private sealed class StaticWebhookPaymentProvider(PaymentWebhookEvent webhookEvent) : IPaymentProvider
    {
        public PaymentWebhookEvent Event { get; } = webhookEvent;

        public string ProviderName => Event.Provider;

        public Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
            PaymentInitiationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
            PaymentVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
            PaymentWebhookParseRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PaymentWebhookEvent>.Success(Event));

        public Task<Result<PaymentRefundResult>> RefundPaymentAsync(
            PaymentRefundRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> VerifyWebhookSignatureAsync(
            PaymentWebhookSignatureVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
