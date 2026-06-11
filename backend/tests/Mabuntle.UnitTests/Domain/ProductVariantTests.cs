using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public class ProductVariantTests
{
    [Fact]
    public void ProductVariant_RequiresPositivePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            0,
            null,
            10));
    }

    [Fact]
    public void ProductVariant_RejectsNegativeStock()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            null,
            -1));
    }

    [Fact]
    public void ProductVariant_ReservedQuantityCannotExceedStock()
    {
        Assert.Throws<InvalidOperationException>(() => new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            null,
            10,
            reservedQuantity: 11));
    }

    [Fact]
    public void ProductVariant_HasSellableStockOnlyWhenActiveAndAvailable()
    {
        var variant = new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            null,
            10);

        variant.Reserve(10);

        Assert.False(variant.HasSellableStock);

        variant.ReleaseReservation(1);

        Assert.True(variant.HasSellableStock);

        variant.Deactivate();

        Assert.False(variant.HasSellableStock);
    }

    [Fact]
    public void ProductVariant_CompareAtPriceMustBeGreaterThanPrice()
    {
        Assert.Throws<ArgumentException>(() => new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            100,
            10));
    }

    [Fact]
    public void ProductVariant_AdjustInventory_UpdatesStockAndStatusWithoutChangingReservations()
    {
        var variant = new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            null,
            10);

        variant.Reserve(3);
        variant.AdjustInventory(5, ProductVariantStatus.OutOfStock);

        Assert.Equal(5, variant.StockQuantity);
        Assert.Equal(3, variant.ReservedQuantity);
        Assert.Equal(2, variant.AvailableQuantity);
        Assert.Equal(ProductVariantStatus.OutOfStock, variant.Status);
    }

    [Fact]
    public void ProductVariant_AdjustInventory_RejectsStockBelowReservedQuantity()
    {
        var variant = new ProductVariant(
            Guid.NewGuid(),
            "SKU-1",
            "M",
            "Black",
            100,
            null,
            10,
            reservedQuantity: 4);

        Assert.Throws<InvalidOperationException>(() => variant.AdjustInventory(3, ProductVariantStatus.Active));
    }
}
