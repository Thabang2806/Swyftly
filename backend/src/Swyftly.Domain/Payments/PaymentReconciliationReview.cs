using Swyftly.Domain.Common;

namespace Swyftly.Domain.Payments;

public sealed class PaymentReconciliationReview : Entity
{
    private PaymentReconciliationReview()
    {
    }

    public PaymentReconciliationReview(
        Guid paymentId,
        string provider,
        string? providerReference,
        string observedProviderStatus,
        decimal? observedAmount,
        string? observedCurrency,
        PaymentReconciliationOutcome outcome,
        string reason,
        Guid reviewedByUserId,
        DateTimeOffset reviewedAtUtc,
        DateTimeOffset? nextReviewAfterUtc)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        }

        if (observedAmount.HasValue && observedAmount.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAmount), "Observed amount must be positive when supplied.");
        }

        PaymentId = paymentId;
        Provider = Required(provider, nameof(provider));
        ProviderReference = Optional(providerReference);
        ObservedProviderStatus = Required(observedProviderStatus, nameof(observedProviderStatus));
        ObservedAmount = observedAmount;
        ObservedCurrency = Optional(observedCurrency)?.ToUpperInvariant();
        Outcome = outcome;
        Reason = Required(reason, nameof(reason));
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        NextReviewAfterUtc = nextReviewAfterUtc;
    }

    public Guid PaymentId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderReference { get; private set; }

    public string ObservedProviderStatus { get; private set; } = string.Empty;

    public decimal? ObservedAmount { get; private set; }

    public string? ObservedCurrency { get; private set; }

    public PaymentReconciliationOutcome Outcome { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid ReviewedByUserId { get; private set; }

    public DateTimeOffset ReviewedAtUtc { get; private set; }

    public DateTimeOffset? NextReviewAfterUtc { get; private set; }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
