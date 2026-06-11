using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Ledger;

namespace Mabuntle.Infrastructure.Ledger;

public sealed class FakePayoutProvider(
    IOptions<PayoutProviderOptions> options,
    TimeProvider timeProvider) : IPayoutProvider
{
    private readonly PayoutProviderOptions _options = options.Value;

    public string ProviderName => _options.ProviderName;

    public Task<Result<PayoutProviderResult>> InitiatePayoutAsync(
        PayoutProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PayoutId == Guid.Empty)
        {
            return Task.FromResult(Result<PayoutProviderResult>.Failure(
                Error.Validation([new("payoutId", "Payout id is required.")])));
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(Result<PayoutProviderResult>.Failure(
                Error.Validation([new("amount", "Payout amount must be positive.")])));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Task.FromResult(Result<PayoutProviderResult>.Failure(
                Error.Validation([new("idempotencyKey", "Payout idempotency key is required.")])));
        }

        return Task.FromResult(Result<PayoutProviderResult>.Success(CreateResult(
            request.IdempotencyKey,
            request.Amount,
            request.Currency,
            _options.FakeOutcome)));
    }

    public Task<Result<PayoutProviderResult>> GetPayoutAsync(
        PayoutProviderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderPayoutReference))
        {
            return Task.FromResult(Result<PayoutProviderResult>.Failure(
                Error.Validation([new("providerPayoutReference", "Provider payout reference is required.")])));
        }

        var outcome = request.ProviderPayoutReference.Contains("failed", StringComparison.OrdinalIgnoreCase)
            ? FakePayoutProviderOutcomes.Failed
            : FakePayoutProviderOutcomes.PaidOut;

        return Task.FromResult(Result<PayoutProviderResult>.Success(CreateResult(
            request.ProviderPayoutReference,
            0m,
            "ZAR",
            outcome)));
    }

    private PayoutProviderResult CreateResult(
        string idempotencyKey,
        decimal amount,
        string currency,
        string outcome)
    {
        var normalizedKey = idempotencyKey.Trim().ToLowerInvariant();
        var status = string.Equals(outcome, FakePayoutProviderOutcomes.Failed, StringComparison.OrdinalIgnoreCase)
            ? FakePayoutProviderOutcomes.Failed
            : string.Equals(outcome, FakePayoutProviderOutcomes.Processing, StringComparison.OrdinalIgnoreCase)
                ? FakePayoutProviderOutcomes.Processing
                : FakePayoutProviderOutcomes.PaidOut;
        var referencePrefix = string.Equals(status, FakePayoutProviderOutcomes.Failed, StringComparison.Ordinal)
            ? "fake_payout_failed"
            : "fake_payout";

        return new PayoutProviderResult(
            ProviderName,
            $"{referencePrefix}_{normalizedKey}",
            status,
            amount,
            currency,
            timeProvider.GetUtcNow(),
            string.Equals(status, FakePayoutProviderOutcomes.Failed, StringComparison.Ordinal)
                ? "The fake payout provider was configured to fail."
                : null);
    }
}
