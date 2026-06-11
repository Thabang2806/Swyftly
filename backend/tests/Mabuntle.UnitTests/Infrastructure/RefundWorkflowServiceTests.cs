using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Payments;
using Mabuntle.Application.Refunds;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Infrastructure.Persistence;
using Mabuntle.Infrastructure.Refunds;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class RefundWorkflowServiceTests
{
    [Fact]
    public async Task ApproveRefundAsync_PersistsProcessingBeforeCallingProvider()
    {
        var databaseName = $"RefundWorkflowServiceTests-{Guid.NewGuid():N}";
        var options = CreateOptions(databaseName);
        await using var dbContext = new MabuntleDbContext(options);
        var seed = await SeedRequestedRefundAsync(dbContext);
        RefundStatus? statusObservedByProvider = null;
        var provider = new CountingRefundProvider(async refundRequest =>
        {
            await using var verificationContext = new MabuntleDbContext(options);
            statusObservedByProvider = await verificationContext.Refunds
                .Where(refund => refund.Id == seed.RefundId)
                .Select(refund => refund.Status)
                .SingleAsync();
        });
        var service = CreateService(dbContext, provider);

        var result = await service.ApproveRefundAsync(CreateApproval(seed.RefundId));

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Processing, statusObservedByProvider);
        Assert.Equal(1, provider.RefundCallCount);
        Assert.Equal("Refunded", result.Value.Status);
        var movement = await dbContext.InventoryMovements.SingleAsync(movement => movement.MovementType == InventoryMovementType.RefundCompleted);
        Assert.Equal(seed.RefundId, movement.RefundId);
        Assert.Equal(seed.PaymentId, movement.PaymentId);
        Assert.Equal(seed.OrderId, movement.OrderId);
        Assert.Equal(0, movement.QuantityDelta);
        Assert.Equal(0, movement.ReservedQuantityAfter - movement.ReservedQuantityBefore);
    }

    [Fact]
    public async Task ApproveRefundAsync_ReturnsExistingRefundedRefundWithoutCallingProvider()
    {
        await using var dbContext = new MabuntleDbContext(CreateOptions($"RefundWorkflowServiceTests-{Guid.NewGuid():N}"));
        var seed = await SeedRequestedRefundAsync(dbContext);
        var firstProvider = new CountingRefundProvider();
        var firstService = CreateService(dbContext, firstProvider);
        var first = await firstService.ApproveRefundAsync(CreateApproval(seed.RefundId));
        Assert.True(first.IsSuccess);
        dbContext.ChangeTracker.Clear();
        var provider = new CountingRefundProvider();
        var service = CreateService(dbContext, provider);

        var result = await service.ApproveRefundAsync(CreateApproval(seed.RefundId));

        Assert.True(result.IsSuccess);
        Assert.Equal(first.Value.ProviderRefundReference, result.Value.ProviderRefundReference);
        Assert.Equal(0, provider.RefundCallCount);
    }

    [Fact]
    public async Task ApproveRefundAsync_ProcessingRefundReturnsConflictWithoutCallingProvider()
    {
        await using var dbContext = new MabuntleDbContext(CreateOptions($"RefundWorkflowServiceTests-{Guid.NewGuid():N}"));
        var seed = await SeedRequestedRefundAsync(dbContext);
        var interruptedProvider = new CountingRefundProvider(_ => throw new InvalidOperationException("Provider call interrupted after processing was persisted."));
        var interruptedService = CreateService(dbContext, interruptedProvider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => interruptedService.ApproveRefundAsync(CreateApproval(seed.RefundId)));
        dbContext.ChangeTracker.Clear();
        var provider = new CountingRefundProvider();
        var service = CreateService(dbContext, provider);

        var result = await service.ApproveRefundAsync(CreateApproval(seed.RefundId));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Refunds.AlreadyProcessing", result.Error.Code);
        Assert.Equal(0, provider.RefundCallCount);
    }

    private static EfRefundWorkflowService CreateService(
        MabuntleDbContext dbContext,
        IPaymentProvider paymentProvider) =>
        new(dbContext, paymentProvider, new NoOpAuditLogService());

    private static ApproveRefundWorkflowRequest CreateApproval(Guid refundId) =>
        new(
            refundId,
            Guid.NewGuid(),
            "Admin",
            "Approved by admin.",
            "127.0.0.1",
            DateTimeOffset.Parse("2026-05-18T12:05:00Z"));

    private static async Task<(Guid RefundId, Guid PaymentId, Guid OrderId)> SeedRequestedRefundAsync(
        MabuntleDbContext dbContext)
    {
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var product = new Product(sellerId);
        var variant = new ProductVariant(product.Id, "SKU-REFUND", "M", "Black", 1000m, 1200m, 5);
        var order = new Order(buyerId, sellerId, Guid.NewGuid(), now);
        order.AddItem(product.Id, variant.Id, "Refundable Item", variant.Sku, variant.Size, variant.Colour, variant.Price, 1);
        order.ChangeStatus(OrderStatus.Paid, now.AddMinutes(1), "TestPaid");
        order.ChangeStatus(OrderStatus.Delivered, now.AddMinutes(2), "TestDelivered");
        var payment = new Payment(order.Id, buyerId, "Fake", 1000m, "ZAR", now);
        payment.SetProviderReference($"fake_{order.Id:N}", now);
        payment.MarkPaid(now);
        var refund = new Refund(order.Id, payment.Id, buyerId, sellerId, null, 500m, "ZAR", "Approved refund.", now);
        var balance = new SellerBalance(sellerId, "ZAR");
        balance.CreditPending(875m);

        dbContext.Products.Add(product);
        dbContext.ProductVariants.Add(variant);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Refunds.Add(refund);
        dbContext.SellerBalances.Add(balance);
        dbContext.LedgerEntries.AddRange(
            new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.BuyerPaymentReceived, 1000m, "ZAR", LedgerDirection.Credit, "Buyer payment received.", now),
            new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PlatformCommissionRecorded, 100m, "ZAR", LedgerDirection.Credit, "Platform commission recorded.", now),
            new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.PaymentProviderFeeRecorded, 25m, "ZAR", LedgerDirection.Debit, "Payment provider fee recorded.", now),
            new LedgerEntry(order.Id, null, sellerId, buyerId, payment.Id, LedgerEntryType.SellerPendingBalanceCredited, 875m, "ZAR", LedgerDirection.Credit, "Seller pending balance credited.", now));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return (refund.Id, payment.Id, order.Id);
    }

    private static DbContextOptions<MabuntleDbContext> CreateOptions(string databaseName) =>
        new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(CreateAuditLogEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CountingRefundProvider(Func<PaymentRefundRequest, Task>? onRefund = null) : IPaymentProvider
    {
        public int RefundCallCount { get; private set; }

        public string ProviderName => "Fake";

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
            throw new NotSupportedException();

        public async Task<Result<PaymentRefundResult>> RefundPaymentAsync(
            PaymentRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            RefundCallCount++;
            if (onRefund is not null)
            {
                await onRefund(request);
            }

            return Result<PaymentRefundResult>.Success(new PaymentRefundResult(
                ProviderName,
                $"provider_refund_{request.IdempotencyKey}",
                "Refunded",
                request.Amount,
                request.Currency,
                DateTimeOffset.Parse("2026-05-18T12:06:00Z")));
        }

        public Task<Result> VerifyWebhookSignatureAsync(
            PaymentWebhookSignatureVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
