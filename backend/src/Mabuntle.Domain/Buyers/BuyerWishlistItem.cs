using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Buyers;

public sealed class BuyerWishlistItem : Entity
{
    private BuyerWishlistItem()
    {
    }

    public BuyerWishlistItem(Guid buyerId, Guid productId, DateTimeOffset createdAtUtc)
    {
        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        BuyerId = buyerId;
        ProductId = productId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BuyerId { get; private set; }

    public Guid ProductId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
