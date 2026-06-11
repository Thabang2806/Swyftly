using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;

namespace Mabuntle.UnitTests.Domain;

public class RefundTests
{
    [Fact]
    public void Constructor_StartsRequestedAndRecordsEvent()
    {
        var requestedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var refund = new Refund(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            500m,
            "zar",
            "Approved return.",
            requestedAt);

        Assert.Equal(RefundStatus.Requested, refund.Status);
        Assert.Equal("ZAR", refund.Currency);
        Assert.Single(refund.Events);
    }

    [Fact]
    public void MarkRefunded_RequiresProcessing()
    {
        var refund = CreateRefund();

        Assert.Throws<InvalidOperationException>(() =>
            refund.MarkRefunded("provider-refund", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Payment_ApplyRefund_MarksPartialOrFull()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), "Fake", 1000m, "ZAR", DateTimeOffset.UtcNow);
        payment.MarkPaid(DateTimeOffset.UtcNow);

        payment.ApplyRefund(500m, DateTimeOffset.UtcNow);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);

        payment.ApplyRefund(1000m, DateTimeOffset.UtcNow);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Payment_MarkPaid_RejectsFailedCancelledAndRefundedStates()
    {
        var failed = new Payment(Guid.NewGuid(), Guid.NewGuid(), "Fake", 1000m, "ZAR", DateTimeOffset.UtcNow);
        failed.MarkFailed(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => failed.MarkPaid(DateTimeOffset.UtcNow));

        var cancelled = new Payment(Guid.NewGuid(), Guid.NewGuid(), "Fake", 1000m, "ZAR", DateTimeOffset.UtcNow);
        cancelled.MarkCancelled(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => cancelled.MarkPaid(DateTimeOffset.UtcNow));

        var refunded = new Payment(Guid.NewGuid(), Guid.NewGuid(), "Fake", 1000m, "ZAR", DateTimeOffset.UtcNow);
        refunded.MarkPaid(DateTimeOffset.UtcNow);
        refunded.ApplyRefund(1000m, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => refunded.MarkPaid(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SellerBalance_ApplyRefundDebit_ConsumesHeldThenPendingAndCanGoNegative()
    {
        var balance = new SellerBalance(Guid.NewGuid(), "ZAR");
        balance.CreditPending(300m);
        balance.HoldPending(200m);

        balance.ApplyRefundDebit(350m);

        Assert.Equal(0m, balance.HeldBalance);
        Assert.Equal(-50m, balance.PendingBalance);

        balance.ApplyRefundDebit(200m);
        Assert.Equal(-250m, balance.PendingBalance);
    }

    private static Refund CreateRefund() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            500m,
            "ZAR",
            "Approved return.",
            DateTimeOffset.UtcNow);
}
