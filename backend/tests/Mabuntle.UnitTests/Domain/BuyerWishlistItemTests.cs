using Mabuntle.Domain.Buyers;

namespace Mabuntle.UnitTests.Domain;

public sealed class BuyerWishlistItemTests
{
    [Fact]
    public void Constructor_CapturesBuyerAndProductForUniquePersistenceKey()
    {
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        var item = new BuyerWishlistItem(buyerId, productId, createdAtUtc);

        Assert.Equal(buyerId, item.BuyerId);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(createdAtUtc, item.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_RejectsEmptyIds()
    {
        Assert.Throws<ArgumentException>(() => new BuyerWishlistItem(Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new BuyerWishlistItem(Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow));
    }
}
