using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public class ProductImageTests
{
    [Fact]
    public void ProductImage_RequiresUrlAndStorageKey()
    {
        Assert.Throws<ArgumentException>(() => new ProductImage(
            Guid.NewGuid(),
            "",
            "storage-key",
            null,
            0,
            false,
            DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => new ProductImage(
            Guid.NewGuid(),
            "https://example.test/image.jpg",
            "",
            null,
            0,
            false,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ProductImage_RejectsNegativeSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductImage(
            Guid.NewGuid(),
            "https://example.test/image.jpg",
            "storage-key",
            null,
            -1,
            false,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ProductImage_CanTogglePrimary()
    {
        var image = new ProductImage(
            Guid.NewGuid(),
            "https://example.test/image.jpg",
            "storage-key",
            "Alt text",
            0,
            false,
            DateTimeOffset.UtcNow);

        image.MarkPrimary();
        Assert.True(image.IsPrimary);

        image.ClearPrimary();
        Assert.False(image.IsPrimary);
    }
}
