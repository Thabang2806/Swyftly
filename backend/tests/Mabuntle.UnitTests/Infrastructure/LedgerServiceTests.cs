using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Ledger;
using Mabuntle.Domain.Ledger;
using Mabuntle.Infrastructure.Ledger;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class LedgerServiceTests
{
    [Fact]
    public async Task CreateSuccessfulPaymentEntriesAsync_CalculatesCommissionAndCreditsSellerPendingBalance()
    {
        await using var dbContext = CreateDbContext();
        var service = new EfLedgerService(dbContext, Options.Create(new LedgerOptions
        {
            PlatformCommissionRatePercent = 10m,
            PaymentProviderFeeRatePercent = 2.5m,
            PaymentProviderFixedFee = 5m
        }));
        var sellerId = Guid.NewGuid();

        var result = await service.CreateSuccessfulPaymentEntriesAsync(new SuccessfulPaymentLedgerRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            sellerId,
            1000m,
            "ZAR",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z")));
        await dbContext.SaveChangesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.PlatformCommissionAmount);
        Assert.Equal(30m, result.Value.PaymentProviderFeeAmount);
        Assert.Equal(870m, result.Value.SellerPendingAmount);
        Assert.Equal(4, await dbContext.LedgerEntries.CountAsync());
        var payout = await dbContext.SellerPayouts.Include(payout => payout.Items).SingleAsync();
        Assert.Equal(870m, payout.Amount);
        Assert.Equal(SellerPayoutStatus.Pending, payout.Status);
        Assert.Single(payout.Items);
        var balance = await dbContext.SellerBalances.SingleAsync();
        Assert.Equal(sellerId, balance.SellerId);
        Assert.Equal(870m, balance.PendingBalance);
    }

    [Fact]
    public async Task CreateSuccessfulPaymentEntriesAsync_IsIdempotentForPayment()
    {
        await using var dbContext = CreateDbContext();
        var service = new EfLedgerService(dbContext, Options.Create(new LedgerOptions()));
        var paymentId = Guid.NewGuid();
        var request = new SuccessfulPaymentLedgerRequest(
            paymentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            "ZAR",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));

        await service.CreateSuccessfulPaymentEntriesAsync(request);
        await dbContext.SaveChangesAsync();
        var second = await service.CreateSuccessfulPaymentEntriesAsync(request);
        await dbContext.SaveChangesAsync();

        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value.EntriesCreated);
        Assert.Equal(4, await dbContext.LedgerEntries.CountAsync(entry => entry.PaymentId == paymentId));
        Assert.Equal(875m, (await dbContext.SellerBalances.SingleAsync()).PendingBalance);
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"LedgerServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new MabuntleDbContext(options);
    }
}
