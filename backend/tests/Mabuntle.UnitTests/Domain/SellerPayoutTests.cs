using Mabuntle.Domain.Ledger;

namespace Mabuntle.UnitTests.Domain;

public class SellerPayoutTests
{
    [Fact]
    public void Hold_ChangesPendingPayoutToOnHold()
    {
        var payout = new SellerPayout(Guid.NewGuid(), 875m, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));

        payout.Hold(Guid.NewGuid().ToString(), "Dispute review.", DateTimeOffset.Parse("2026-05-18T12:05:00Z"));

        Assert.Equal(SellerPayoutStatus.OnHold, payout.Status);
        Assert.Equal("Dispute review.", payout.HoldReason);
        Assert.NotNull(payout.HeldAtUtc);
    }

    [Fact]
    public void Release_RequiresPayoutToBeOnHold()
    {
        var payout = new SellerPayout(Guid.NewGuid(), 875m, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));

        Assert.Throws<InvalidOperationException>(() =>
            payout.Release(Guid.NewGuid().ToString(), "Reviewed.", DateTimeOffset.Parse("2026-05-18T12:05:00Z")));
    }

    [Fact]
    public void Release_ReturnsHeldPayoutToPending()
    {
        var payout = new SellerPayout(Guid.NewGuid(), 875m, "ZAR", DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        payout.Hold(Guid.NewGuid().ToString(), "Admin review.", DateTimeOffset.Parse("2026-05-18T12:05:00Z"));

        payout.Release(Guid.NewGuid().ToString(), "Review complete.", DateTimeOffset.Parse("2026-05-18T12:10:00Z"));

        Assert.Equal(SellerPayoutStatus.Pending, payout.Status);
        Assert.Equal("Review complete.", payout.ReleaseReason);
        Assert.NotNull(payout.ReleasedAtUtc);
    }
}
