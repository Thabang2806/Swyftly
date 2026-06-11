using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Ledger;
using Mabuntle.Domain.Ledger;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Ledger;

public sealed class EfLedgerService(
    MabuntleDbContext dbContext,
    IOptions<LedgerOptions> options) : ILedgerService
{
    private readonly LedgerOptions _options = options.Value;

    public async Task<Result<SuccessfulPaymentLedgerResult>> CreateSuccessfulPaymentEntriesAsync(
        SuccessfulPaymentLedgerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailures = Validate(request);
        if (validationFailures.Count > 0)
        {
            return Result<SuccessfulPaymentLedgerResult>.Failure(Error.Validation(validationFailures));
        }

        var existingEntries = await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == request.PaymentId)
            .ToListAsync(cancellationToken);
        if (existingEntries.Count > 0)
        {
            return Result<SuccessfulPaymentLedgerResult>.Success(new SuccessfulPaymentLedgerResult(
                existingEntries
                    .Where(entry => entry.Type == LedgerEntryType.PlatformCommissionRecorded)
                    .Sum(entry => entry.Amount),
                existingEntries
                    .Where(entry => entry.Type == LedgerEntryType.PaymentProviderFeeRecorded)
                    .Sum(entry => entry.Amount),
                existingEntries
                    .Where(entry => entry.Type == LedgerEntryType.SellerPendingBalanceCredited)
                    .Sum(entry => entry.Amount),
                EntriesCreated: 0));
        }

        var platformCommission = RoundMoney(request.Amount * _options.PlatformCommissionRatePercent / 100m);
        var providerFee = RoundMoney(request.Amount * _options.PaymentProviderFeeRatePercent / 100m + _options.PaymentProviderFixedFee);
        var sellerPendingAmount = Math.Max(0, request.Amount - platformCommission - providerFee);

        var buyerPaymentEntry = new LedgerEntry(
                request.OrderId,
                null,
                request.SellerId,
                request.BuyerId,
                request.PaymentId,
                LedgerEntryType.BuyerPaymentReceived,
                request.Amount,
                request.Currency,
                LedgerDirection.Credit,
                "Buyer payment received.",
                request.CreatedAtUtc);
        var platformCommissionEntry = new LedgerEntry(
                request.OrderId,
                null,
                request.SellerId,
                request.BuyerId,
                request.PaymentId,
                LedgerEntryType.PlatformCommissionRecorded,
                platformCommission,
                request.Currency,
                LedgerDirection.Credit,
                "Platform commission recorded.",
                request.CreatedAtUtc);
        var providerFeeEntry = new LedgerEntry(
                request.OrderId,
                null,
                request.SellerId,
                request.BuyerId,
                request.PaymentId,
                LedgerEntryType.PaymentProviderFeeRecorded,
                providerFee,
                request.Currency,
                LedgerDirection.Debit,
                "Payment provider fee recorded.",
                request.CreatedAtUtc);
        var sellerPendingEntry = new LedgerEntry(
                request.OrderId,
                null,
                request.SellerId,
                request.BuyerId,
                request.PaymentId,
                LedgerEntryType.SellerPendingBalanceCredited,
                sellerPendingAmount,
                request.Currency,
                LedgerDirection.Credit,
                "Seller pending balance credited.",
                request.CreatedAtUtc);

        dbContext.LedgerEntries.AddRange(
            buyerPaymentEntry,
            platformCommissionEntry,
            providerFeeEntry,
            sellerPendingEntry);

        var payout = new SellerPayout(request.SellerId, sellerPendingAmount, request.Currency, request.CreatedAtUtc);
        payout.AddItem(sellerPendingEntry.Id, request.OrderId, request.PaymentId, sellerPendingAmount, request.CreatedAtUtc);
        dbContext.SellerPayouts.Add(payout);

        var balance = await dbContext.SellerBalances.SingleOrDefaultAsync(
            sellerBalance => sellerBalance.SellerId == request.SellerId && sellerBalance.Currency == request.Currency,
            cancellationToken);
        if (balance is null)
        {
            balance = new SellerBalance(request.SellerId, request.Currency);
            dbContext.SellerBalances.Add(balance);
        }

        balance.CreditPending(sellerPendingAmount);

        return Result<SuccessfulPaymentLedgerResult>.Success(new SuccessfulPaymentLedgerResult(
            platformCommission,
            providerFee,
            sellerPendingAmount,
            EntriesCreated: 4));
    }

    private static List<ValidationFailure> Validate(SuccessfulPaymentLedgerRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.PaymentId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("paymentId", "Payment id is required."));
        }

        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (request.BuyerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerId", "Buyer id is required."));
        }

        if (request.SellerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("sellerId", "Seller id is required."));
        }

        if (request.Amount <= 0)
        {
            failures.Add(new ValidationFailure("amount", "Amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            failures.Add(new ValidationFailure("currency", "Currency is required."));
        }

        return failures;
    }

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
