using Mabuntle.Domain.Carts;

namespace Mabuntle.UnitTests.Domain;

public class CartTests
{
    [Fact]
    public void AddOrUpdateItem_AddsItemAndCapturesPrice()
    {
        var cart = new Cart(Guid.NewGuid());
        var sellerId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        cart.AddOrUpdateItem(
            Guid.NewGuid(),
            variantId,
            sellerId,
            "Cotton Dress",
            "SKU-1",
            "M",
            "Black",
            499m,
            2,
            availableQuantity: 5);

        var item = Assert.Single(cart.Items);
        Assert.Equal(sellerId, cart.SellerId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(499m, item.UnitPrice);
        Assert.Equal(998m, item.LineTotal);
    }

    [Fact]
    public void AddOrUpdateItem_IncrementsExistingVariantQuantity()
    {
        var cart = new Cart(Guid.NewGuid());
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        cart.AddOrUpdateItem(productId, variantId, sellerId, "Dress", "SKU-1", "M", "Black", 100m, 1, 5);
        cart.AddOrUpdateItem(productId, variantId, sellerId, "Dress", "SKU-1", "M", "Black", 100m, 2, 5);

        var item = Assert.Single(cart.Items);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void AddOrUpdateItem_RejectsMixedSellerCart()
    {
        var cart = new Cart(Guid.NewGuid());

        cart.AddOrUpdateItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Dress", "SKU-1", "M", "Black", 100m, 1, 5);

        Assert.Throws<InvalidOperationException>(() => cart.AddOrUpdateItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Shoes",
            "SKU-2",
            "8",
            "White",
            250m,
            1,
            5));
    }

    [Fact]
    public void AddOrUpdateItem_RejectsQuantityAboveAvailableStock()
    {
        var cart = new Cart(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => cart.AddOrUpdateItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dress",
            "SKU-1",
            "M",
            "Black",
            100m,
            6,
            availableQuantity: 5));
    }

    [Fact]
    public void RemoveItem_ClearsSellerWhenCartBecomesEmpty()
    {
        var cart = new Cart(Guid.NewGuid());
        cart.AddOrUpdateItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Dress", "SKU-1", "M", "Black", 100m, 1, 5);
        var itemId = Assert.Single(cart.Items).Id;

        cart.RemoveItem(itemId);

        Assert.Empty(cart.Items);
        Assert.Null(cart.SellerId);
    }
}
