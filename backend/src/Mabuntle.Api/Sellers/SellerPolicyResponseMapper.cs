using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;

namespace Mabuntle.Api.Sellers;

public static class SellerPolicyResponseMapper
{
    private static readonly (string Property, string Label)[] RequiredFields =
    [
        (nameof(SellerStorePolicy.ReturnWindowDays), "Return window"),
        (nameof(SellerStorePolicy.ReturnPolicy), "Return policy"),
        (nameof(SellerStorePolicy.ExchangePolicy), "Exchange policy"),
        (nameof(SellerStorePolicy.FulfilmentPolicy), "Fulfilment policy"),
        (nameof(SellerStorePolicy.SupportPolicy), "Support policy")
    ];

    public static SellerPolicyResponse Map(SellerStorePolicy? policy)
    {
        var missingFields = MissingFields(policy);
        return new SellerPolicyResponse(
            policy?.ReturnWindowDays,
            policy?.ReturnPolicy,
            policy?.ExchangePolicy,
            policy?.FulfilmentPolicy,
            policy?.SupportPolicy,
            policy?.CareInstructions,
            policy?.ProductDisclaimer,
            missingFields.Count == 0,
            missingFields,
            policy?.UpdatedAtUtc);
    }

    public static SellerPolicySnapshotResponse? MapSnapshot(OrderSellerPolicySnapshot? snapshot) =>
        snapshot is null
            ? null
            : new SellerPolicySnapshotResponse(
                snapshot.ReturnWindowDays,
                snapshot.ReturnPolicy,
                snapshot.ExchangePolicy,
                snapshot.FulfilmentPolicy,
                snapshot.SupportPolicy,
                snapshot.CareInstructions,
                snapshot.ProductDisclaimer,
                snapshot.SnapshotAtUtc);

    private static IReadOnlyCollection<string> MissingFields(SellerStorePolicy? policy)
    {
        if (policy is null)
        {
            return RequiredFields.Select(field => field.Label).ToArray();
        }

        var missing = new List<string>();
        if (!policy.ReturnWindowDays.HasValue)
        {
            missing.Add("Return window");
        }

        if (string.IsNullOrWhiteSpace(policy.ReturnPolicy))
        {
            missing.Add("Return policy");
        }

        if (string.IsNullOrWhiteSpace(policy.ExchangePolicy))
        {
            missing.Add("Exchange policy");
        }

        if (string.IsNullOrWhiteSpace(policy.FulfilmentPolicy))
        {
            missing.Add("Fulfilment policy");
        }

        if (string.IsNullOrWhiteSpace(policy.SupportPolicy))
        {
            missing.Add("Support policy");
        }

        return missing;
    }
}
