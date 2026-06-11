namespace Mabuntle.Application.Advertising;

public interface IAdCampaignEligibilityService
{
    Task<AdCampaignEligibilityResult> ValidateAsync(
        Guid sellerId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}

public sealed record AdCampaignEligibilityResult(
    Guid SellerId,
    bool IsEligible,
    IReadOnlyCollection<AdProductEligibilityResult> Products,
    IReadOnlyCollection<string> SellerReasons)
{
    public IReadOnlyCollection<string> AllReasons =>
        SellerReasons.Concat(Products.SelectMany(product => product.Reasons)).ToArray();
}

public sealed record AdProductEligibilityResult(
    Guid ProductId,
    bool IsEligible,
    int QualityScore,
    IReadOnlyCollection<string> Reasons);
