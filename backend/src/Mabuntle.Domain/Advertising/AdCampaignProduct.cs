using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Advertising;

public sealed class AdCampaignProduct : Entity
{
    private AdCampaignProduct()
    {
    }

    public AdCampaignProduct(Guid adCampaignId, Guid productId, DateTimeOffset createdAtUtc)
    {
        if (adCampaignId == Guid.Empty)
        {
            throw new ArgumentException("Ad campaign id is required.", nameof(adCampaignId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        AdCampaignId = adCampaignId;
        ProductId = productId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid AdCampaignId { get; private set; }

    public Guid ProductId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
