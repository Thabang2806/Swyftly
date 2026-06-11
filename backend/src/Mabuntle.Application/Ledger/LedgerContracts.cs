using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Ledger;

public interface ILedgerService
{
    Task<Result<SuccessfulPaymentLedgerResult>> CreateSuccessfulPaymentEntriesAsync(
        SuccessfulPaymentLedgerRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SuccessfulPaymentLedgerRequest(
    Guid PaymentId,
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc);

public sealed record SuccessfulPaymentLedgerResult(
    decimal PlatformCommissionAmount,
    decimal PaymentProviderFeeAmount,
    decimal SellerPendingAmount,
    int EntriesCreated);

public sealed class LedgerOptions
{
    public const string SectionName = "Ledger";

    public decimal PlatformCommissionRatePercent { get; set; } = 10m;

    public decimal PaymentProviderFeeRatePercent { get; set; } = 2.5m;

    public decimal PaymentProviderFixedFee { get; set; } = 0m;
}
