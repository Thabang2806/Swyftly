using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerAiDiscoveryPreference : Entity
{
    private BuyerAiDiscoveryPreference()
    {
    }

    public BuyerAiDiscoveryPreference(Guid buyerId, bool historyEnabled, DateTimeOffset updatedAtUtc, bool personalizationEnabled = false)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        BuyerId = buyerId;
        SetPreferences(historyEnabled, personalizationEnabled, updatedAtUtc);
    }

    public Guid BuyerId { get; private set; }

    public bool HistoryEnabled { get; private set; }

    public bool PersonalizationEnabled { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void SetHistoryEnabled(bool historyEnabled, DateTimeOffset updatedAtUtc)
    {
        SetPreferences(historyEnabled, PersonalizationEnabled, updatedAtUtc);
    }

    public void SetPreferences(bool historyEnabled, bool personalizationEnabled, DateTimeOffset updatedAtUtc)
    {
        HistoryEnabled = historyEnabled;
        PersonalizationEnabled = personalizationEnabled;
        UpdatedAtUtc = updatedAtUtc;
    }
}
