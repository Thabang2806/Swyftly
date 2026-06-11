using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ledger;

public sealed class SellerPayout : AuditableEntity
{
    private readonly List<SellerPayoutItem> _items = [];

    private SellerPayout()
    {
    }

    public SellerPayout(Guid sellerId, decimal amount, string currency, DateTimeOffset createdAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        SellerId = sellerId;
        Amount = amount;
        Currency = Required(currency, nameof(currency)).ToUpperInvariant();
        Status = SellerPayoutStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public SellerPayoutStatus Status { get; private set; }

    public SellerPayoutStatus? HeldFromStatus { get; private set; }

    public DateTimeOffset? HeldAtUtc { get; private set; }

    public string? HeldByUserId { get; private set; }

    public string? HoldReason { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public string? ReleasedByUserId { get; private set; }

    public string? ReleaseReason { get; private set; }

    public DateTimeOffset? AvailableAtUtc { get; private set; }

    public string? AvailableByUserId { get; private set; }

    public string? AvailabilityReason { get; private set; }

    public DateTimeOffset? ProcessingAtUtc { get; private set; }

    public string? ProcessingByUserId { get; private set; }

    public string? ProcessingReason { get; private set; }

    public DateTimeOffset? PaidOutAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public string? ProviderName { get; private set; }

    public string? ProviderPayoutReference { get; private set; }

    public string? ProviderStatus { get; private set; }

    public int ConcurrencyVersion { get; private set; }

    public IReadOnlyCollection<SellerPayoutItem> Items => _items.AsReadOnly();

    public void AddItem(Guid ledgerEntryId, Guid? orderId, Guid? paymentId, decimal amount, DateTimeOffset createdAtUtc)
    {
        if (ledgerEntryId == Guid.Empty)
        {
            throw new ArgumentException("Ledger entry id is required.", nameof(ledgerEntryId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        _items.Add(new SellerPayoutItem(Id, ledgerEntryId, orderId, paymentId, amount, Currency, createdAtUtc));
    }

    public void MakeAvailable(string actorUserId, string reason, DateTimeOffset availableAtUtc)
    {
        if (Status == SellerPayoutStatus.Available)
        {
            return;
        }

        if (Status != SellerPayoutStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payouts can be made available.");
        }

        Status = SellerPayoutStatus.Available;
        AvailableByUserId = Required(actorUserId, nameof(actorUserId));
        AvailabilityReason = Required(reason, nameof(reason));
        AvailableAtUtc = availableAtUtc;
        UpdatedAtUtc = availableAtUtc;
        ConcurrencyVersion++;
    }

    public void Hold(string actorUserId, string reason, DateTimeOffset heldAtUtc)
    {
        if (Status is SellerPayoutStatus.OnHold)
        {
            return;
        }

        if (Status is not (SellerPayoutStatus.Pending or SellerPayoutStatus.Available))
        {
            throw new InvalidOperationException("Only pending or available payouts can be held.");
        }

        HeldFromStatus = Status;
        Status = SellerPayoutStatus.OnHold;
        HeldByUserId = Required(actorUserId, nameof(actorUserId));
        HoldReason = Required(reason, nameof(reason));
        HeldAtUtc = heldAtUtc;
        UpdatedAtUtc = heldAtUtc;
        ConcurrencyVersion++;
    }

    public void Release(string actorUserId, string reason, DateTimeOffset releasedAtUtc)
    {
        if (Status != SellerPayoutStatus.OnHold)
        {
            throw new InvalidOperationException("Only held payouts can be released.");
        }

        Status = HeldFromStatus ?? SellerPayoutStatus.Pending;
        ReleasedByUserId = Required(actorUserId, nameof(actorUserId));
        ReleaseReason = Required(reason, nameof(reason));
        ReleasedAtUtc = releasedAtUtc;
        HeldFromStatus = null;
        UpdatedAtUtc = releasedAtUtc;
        ConcurrencyVersion++;
    }

    public void StartProcessing(string actorUserId, string reason, DateTimeOffset processingAtUtc)
    {
        if (Status is not (SellerPayoutStatus.Available or SellerPayoutStatus.Failed))
        {
            throw new InvalidOperationException("Only available or failed payouts can be processed.");
        }

        Status = SellerPayoutStatus.Processing;
        ProcessingByUserId = Required(actorUserId, nameof(actorUserId));
        ProcessingReason = Required(reason, nameof(reason));
        ProcessingAtUtc = processingAtUtc;
        FailureReason = null;
        UpdatedAtUtc = processingAtUtc;
        ConcurrencyVersion++;
    }

    public void RecordProviderProcessing(string providerName, string providerPayoutReference, string providerStatus, DateTimeOffset processedAtUtc)
    {
        if (Status != SellerPayoutStatus.Processing)
        {
            throw new InvalidOperationException("Only processing payouts can receive provider processing data.");
        }

        ProviderName = Required(providerName, nameof(providerName));
        ProviderPayoutReference = Required(providerPayoutReference, nameof(providerPayoutReference));
        ProviderStatus = Required(providerStatus, nameof(providerStatus));
        UpdatedAtUtc = processedAtUtc;
        ConcurrencyVersion++;
    }

    public void MarkPaidOut(string providerName, string providerPayoutReference, string providerStatus, DateTimeOffset paidOutAtUtc)
    {
        if (Status != SellerPayoutStatus.Processing)
        {
            throw new InvalidOperationException("Only processing payouts can be marked paid out.");
        }

        ProviderName = Required(providerName, nameof(providerName));
        ProviderPayoutReference = Required(providerPayoutReference, nameof(providerPayoutReference));
        ProviderStatus = Required(providerStatus, nameof(providerStatus));
        Status = SellerPayoutStatus.PaidOut;
        PaidOutAtUtc = paidOutAtUtc;
        UpdatedAtUtc = paidOutAtUtc;
        ConcurrencyVersion++;
    }

    public void MarkFailed(string providerName, string providerPayoutReference, string providerStatus, string failureReason, DateTimeOffset failedAtUtc)
    {
        if (Status != SellerPayoutStatus.Processing)
        {
            throw new InvalidOperationException("Only processing payouts can be marked failed.");
        }

        ProviderName = Required(providerName, nameof(providerName));
        ProviderPayoutReference = Required(providerPayoutReference, nameof(providerPayoutReference));
        ProviderStatus = Required(providerStatus, nameof(providerStatus));
        Status = SellerPayoutStatus.Failed;
        FailureReason = Required(failureReason, nameof(failureReason));
        FailedAtUtc = failedAtUtc;
        UpdatedAtUtc = failedAtUtc;
        ConcurrencyVersion++;
    }

    public void ApplyAdjustment(decimal amount, DateTimeOffset adjustedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > Amount)
        {
            throw new InvalidOperationException("Adjustment cannot exceed the payout amount.");
        }

        Amount -= amount;
        if (Amount == 0 && Status is SellerPayoutStatus.Pending or SellerPayoutStatus.OnHold or SellerPayoutStatus.Available)
        {
            Status = SellerPayoutStatus.Reversed;
        }

        UpdatedAtUtc = adjustedAtUtc;
        ConcurrencyVersion++;
    }

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }
}
