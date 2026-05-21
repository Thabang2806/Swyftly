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
using Swyftly.Api.Admin;
using Swyftly.Api.Authentication;
using Swyftly.Application.Identity;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.IntegrationTests;

public sealed class AdminOrderPaymentTests
{
    [Fact]
    public async Task Buyer_CannotReadAdminOrderPaymentRecords()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var buyerToken = await RegisterAndLoginBuyerAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.GetAsync("/api/admin/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadOrderAndPaymentListAndDetail()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedOrderPaymentDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var ordersResponse = await client.GetAsync("/api/admin/orders");
        ordersResponse.EnsureSuccessStatusCode();
        var orders = await ordersResponse.Content.ReadFromJsonAsync<AdminOrderSummaryResponse[]>();
        Assert.NotNull(orders);
        var orderSummary = Assert.Single(orders!);
        Assert.Equal(seed.OrderId, orderSummary.OrderId);
        Assert.Equal("Seller Store", orderSummary.SellerDisplayName);
        Assert.Equal("Paid", orderSummary.PaymentStatus);

        using var orderResponse = await client.GetAsync($"/api/admin/orders/{seed.OrderId}");
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<AdminOrderDetailResponse>();
        Assert.NotNull(order);
        Assert.Equal(seed.OrderId, order!.OrderId);
        Assert.Single(order.Items);
        Assert.Single(order.Payments);

        using var paymentsResponse = await client.GetAsync($"/api/admin/payments?orderId={seed.OrderId}");
        paymentsResponse.EnsureSuccessStatusCode();
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<AdminPaymentSummaryResponse[]>();
        Assert.NotNull(payments);
        var paymentSummary = Assert.Single(payments!);
        Assert.Equal(seed.PaymentId, paymentSummary.PaymentId);
        Assert.Equal("fake-pay", paymentSummary.Provider);

        using var paymentResponse = await client.GetAsync($"/api/admin/payments/{seed.PaymentId}");
        paymentResponse.EnsureSuccessStatusCode();
        var payment = await paymentResponse.Content.ReadFromJsonAsync<AdminPaymentDetailResponse>();
        Assert.NotNull(payment);
        Assert.Equal(seed.PaymentId, payment!.PaymentId);
        Assert.Equal(seed.OrderId, payment.Order!.OrderId);
        var paymentEvent = Assert.Single(payment.Events);
        Assert.Equal("payment.captured", paymentEvent.EventType);
        Assert.Equal("Processed", paymentEvent.ProcessingStatus);
    }

    [Fact]
    public async Task Admin_CanReadPaymentReconciliationCandidates()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var adminToken = await CreateAndLoginAdminAsync(factory, client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await client.GetAsync("/api/admin/payments/reconciliation-candidates?olderThanMinutes=30");

        response.EnsureSuccessStatusCode();
        var candidates = await response.Content.ReadFromJsonAsync<AdminPaymentReconciliationCandidateResponse[]>();
        Assert.NotNull(candidates);
        Assert.Equal(2, candidates!.Length);
        Assert.Contains(candidates, candidate =>
            candidate.PaymentId == seed.StalePendingPaymentId
            && candidate.ReasonCode == "StalePendingPayment");
        var failedCandidate = Assert.Single(candidates, candidate => candidate.PaymentId == seed.FailedWebhookPaymentId);
        Assert.Equal("FailedWebhookEvent", failedCandidate.ReasonCode);
        Assert.NotNull(failedCandidate.LatestEvent);
        Assert.Equal("Failed", failedCandidate.LatestEvent!.ProcessingStatus);
        Assert.DoesNotContain(candidates, candidate => candidate.PaymentId == seed.FreshPendingPaymentId);
    }

    [Fact]
    public async Task FinanceApprover_CanRecordPaymentReconciliationReview()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var financeToken = await CreateAndLoginAdminAsync(factory, client, SwyftlyRoles.FinanceApprover);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", financeToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "PENDING",
                observedAmount = 145.00m,
                observedCurrency = "zar",
                outcome = "ProviderPending",
                reason = "Provider dashboard still shows pending checkout."
            });

        response.EnsureSuccessStatusCode();
        var review = await response.Content.ReadFromJsonAsync<AdminPaymentReconciliationReviewResponse>();
        Assert.NotNull(review);
        Assert.Equal(seed.StalePendingPaymentId, review!.PaymentId);
        Assert.Equal("ProviderPending", review.Outcome);
        Assert.Equal("ZAR", review.ObservedCurrency);

        using var candidatesResponse = await client.GetAsync("/api/admin/payments/reconciliation-candidates?olderThanMinutes=30");
        candidatesResponse.EnsureSuccessStatusCode();
        var candidates = await candidatesResponse.Content.ReadFromJsonAsync<AdminPaymentReconciliationCandidateResponse[]>();
        var candidate = Assert.Single(candidates!, candidate => candidate.PaymentId == seed.StalePendingPaymentId);
        Assert.NotNull(candidate.LatestReview);
        Assert.Equal(review.ReviewId, candidate.LatestReview!.ReviewId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        Assert.Single(dbContext.PaymentReconciliationReviews.Where(item => item.PaymentId == seed.StalePendingPaymentId));
        Assert.Contains(dbContext.AuditLogs, log =>
            log.ActionType == "PaymentReconciliationReviewed"
            && log.EntityId == seed.StalePendingPaymentId.ToString());
    }

    [Fact]
    public async Task FinanceOperator_CannotRecordPaymentReconciliationReview()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var operatorToken = await CreateAndLoginAdminAsync(factory, client, SwyftlyRoles.FinanceOperator);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "COMPLETE",
                observedAmount = 145.00m,
                observedCurrency = "ZAR",
                outcome = "ProviderPaidMissingWebhook",
                reason = "Provider dashboard shows complete."
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReconciliationReview_ValidatesOutcomeAndReason()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var financeToken = await CreateAndLoginAdminAsync(factory, client, SwyftlyRoles.FinanceApprover);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", financeToken);

        using var invalidOutcomeResponse = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "COMPLETE",
                observedAmount = 145.00m,
                observedCurrency = "ZAR",
                outcome = "Settled",
                reason = "Provider dashboard shows complete."
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidOutcomeResponse.StatusCode);

        using var emptyReasonResponse = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "COMPLETE",
                observedAmount = 145.00m,
                observedCurrency = "ZAR",
                outcome = "ProviderPaidMissingWebhook",
                reason = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, emptyReasonResponse.StatusCode);
    }

    [Fact]
    public async Task ReconciliationCandidates_HideSnoozedReviewsUnlessRequested()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var financeToken = await CreateAndLoginAdminAsync(factory, client, SwyftlyRoles.FinanceApprover);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", financeToken);

        using var reviewResponse = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "PENDING",
                observedAmount = 145.00m,
                observedCurrency = "ZAR",
                outcome = "ProviderPending",
                reason = "Provider dashboard is still pending; review tomorrow.",
                nextReviewAfterUtc = DateTimeOffset.UtcNow.AddDays(1)
            });
        reviewResponse.EnsureSuccessStatusCode();

        using var defaultResponse = await client.GetAsync("/api/admin/payments/reconciliation-candidates?olderThanMinutes=30");
        defaultResponse.EnsureSuccessStatusCode();
        var defaultCandidates = await defaultResponse.Content.ReadFromJsonAsync<AdminPaymentReconciliationCandidateResponse[]>();
        Assert.DoesNotContain(defaultCandidates!, candidate => candidate.PaymentId == seed.StalePendingPaymentId);

        using var includeSnoozedResponse = await client.GetAsync("/api/admin/payments/reconciliation-candidates?olderThanMinutes=30&includeSnoozed=true");
        includeSnoozedResponse.EnsureSuccessStatusCode();
        var snoozedCandidates = await includeSnoozedResponse.Content.ReadFromJsonAsync<AdminPaymentReconciliationCandidateResponse[]>();
        Assert.Contains(snoozedCandidates!, candidate =>
            candidate.PaymentId == seed.StalePendingPaymentId
            && candidate.LatestReview?.Outcome == "ProviderPending");
    }

    [Fact]
    public async Task ProviderPaidMissingWebhookReview_DoesNotSettlePaymentOrOrder()
    {
        using var factory = new AdminOrderPaymentTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedReconciliationCandidateDataAsync(factory);
        var financeToken = await CreateAndLoginAdminAsync(factory, client, SwyftlyRoles.FinanceApprover);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", financeToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/payments/{seed.StalePendingPaymentId}/reconciliation-reviews",
            new
            {
                observedProviderStatus = "COMPLETE",
                observedAmount = 145.00m,
                observedCurrency = "ZAR",
                outcome = "ProviderPaidMissingWebhook",
                reason = "Provider dashboard shows COMPLETE but no valid ITN was received."
            });
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var payment = await dbContext.Payments.SingleAsync(payment => payment.Id == seed.StalePendingPaymentId);
        var order = await dbContext.Orders.SingleAsync(order => order.Id == payment.OrderId);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Empty(dbContext.LedgerEntries);
    }

    private static async Task<OrderPaymentSeed> SeedOrderPaymentDataAsync(AdminOrderPaymentTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var now = DateTimeOffset.UtcNow;

        var buyerId = Guid.NewGuid();
        var sellerUserId = Guid.NewGuid();
        var seller = new SellerProfile(sellerUserId);
        seller.UpdateProfile(
            "Seller Store",
            "seller-orders@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Seller Trading");

        var order = new Order(buyerId, seller.Id, Guid.NewGuid(), now, shippingAmount: 25m);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Summer Dress", "SKU-1", "M", "Red", 120m, 2);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "PaymentCaptured");

        var payment = new Payment(order.Id, buyerId, "fake-pay", order.TotalAmount, "ZAR", now);
        payment.SetProviderReference("provider-payment-1", now.AddMinutes(1));
        payment.MarkPaid(now.AddMinutes(1));

        var paymentEvent = new PaymentEvent(
            payment.Id,
            "fake-pay",
            "provider-event-1",
            "payment.captured",
            "{\"status\":\"captured\"}",
            now.AddMinutes(1));
        paymentEvent.MarkProcessed(payment.Id, now.AddMinutes(1));

        dbContext.AddRange(seller, order, payment, paymentEvent);
        await dbContext.SaveChangesAsync();

        return new OrderPaymentSeed(order.Id, payment.Id);
    }

    private static async Task<ReconciliationSeed> SeedReconciliationCandidateDataAsync(AdminOrderPaymentTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SwyftlyDbContext>();
        var now = DateTimeOffset.UtcNow;

        var buyerId = Guid.NewGuid();
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Reconciliation Seller",
            "seller-reconciliation@example.test",
            "+27110000001",
            SellerBusinessType.RegisteredBusiness,
            "Reconciliation Trading");

        var staleOrder = CreateOrder(buyerId, seller.Id, now.AddHours(-2));
        var stalePayment = new Payment(staleOrder.Id, buyerId, "PayFast", staleOrder.TotalAmount, "ZAR", now.AddHours(-2));
        stalePayment.SetProviderReference("stale-provider-reference", now.AddHours(-2));

        factory.Clock.SetUtcNow(now.AddHours(-2));
        dbContext.AddRange(seller, staleOrder, stalePayment);
        await dbContext.SaveChangesAsync();

        factory.Clock.SetUtcNow(now);
        var freshOrder = CreateOrder(buyerId, seller.Id, now);
        var freshPayment = new Payment(freshOrder.Id, buyerId, "PayFast", freshOrder.TotalAmount, "ZAR", now);
        freshPayment.SetProviderReference("fresh-provider-reference", now);

        var failedEventOrder = CreateOrder(buyerId, seller.Id, now.AddMinutes(-10));
        var failedEventPayment = new Payment(failedEventOrder.Id, buyerId, "PayFast", failedEventOrder.TotalAmount, "ZAR", now.AddMinutes(-10));
        failedEventPayment.SetProviderReference("failed-event-provider-reference", now.AddMinutes(-10));
        var failedEvent = new PaymentEvent(
            failedEventPayment.Id,
            "PayFast",
            "failed-provider-event",
            "payment.complete",
            "{\"payment_status\":\"COMPLETE\"}",
            now.AddMinutes(-9));
        failedEvent.MarkFailed("Amount mismatch.", now.AddMinutes(-9));

        dbContext.AddRange(freshOrder, freshPayment, failedEventOrder, failedEventPayment, failedEvent);
        await dbContext.SaveChangesAsync();

        return new ReconciliationSeed(stalePayment.Id, freshPayment.Id, failedEventPayment.Id);
    }

    private static Order CreateOrder(Guid buyerId, Guid sellerId, DateTimeOffset createdAtUtc)
    {
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), createdAtUtc, shippingAmount: 25m);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Summer Dress", "SKU-1", "M", "Red", 120m, 1);
        return order;
    }

    private static async Task<string> RegisterAndLoginBuyerAsync(HttpClient client)
    {
        const string email = "buyer-admin-order-payments@example.test";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", SwyftlyRoles.Buyer));
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    private static async Task<string> CreateAndLoginAdminAsync(
        AdminOrderPaymentTestFactory factory,
        HttpClient client,
        params string[] roles)
    {
        var email = $"admin-order-payments-{Guid.NewGuid():N}@example.test";
        var assignedRoles = roles.Length == 0 ? [SwyftlyRoles.Admin] : roles;

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

            foreach (var role in assignedRoles)
            {
                var roleResult = await userManager.AddToRoleAsync(admin, role);
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
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

    private sealed record OrderPaymentSeed(Guid OrderId, Guid PaymentId);

    private sealed record ReconciliationSeed(
        Guid StalePendingPaymentId,
        Guid FreshPendingPaymentId,
        Guid FailedWebhookPaymentId);

    private sealed class AdminOrderPaymentTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"SwyftlyAdminOrderPaymentTests_{Guid.NewGuid():N}";

        public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SwyftlyDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwyftlyDbContext>>();
                services.RemoveAll<TimeProvider>();

                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
                services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
                {
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
                });
            });
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }
}
