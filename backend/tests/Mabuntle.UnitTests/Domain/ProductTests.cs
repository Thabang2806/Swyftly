using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Product_StartsAsDraft()
    {
        var product = new Product(Guid.NewGuid());

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.True(product.CanSellerEdit);
    }

    [Fact]
    public void SellerCanEditOnlyDraftOrRejectedProducts()
    {
        var product = CreateSubmittableProduct();
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);

        Assert.False(product.CanSellerEdit);
        Assert.Throws<InvalidOperationException>(() => product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "New title",
            "new-title",
            "Short",
            "Full"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SubmitForReview_RequiresImageAndActiveVariant(
        bool hasAtLeastOneImage,
        bool hasAtLeastOneActiveVariant)
    {
        var product = CreateSubmittableProduct();

        Assert.Throws<InvalidOperationException>(() => product.SubmitForReview(
            hasAtLeastOneImage,
            hasAtLeastOneActiveVariant));
    }

    [Fact]
    public void SubmitForReview_RequiresCategoryTitleSlugAndDescriptions()
    {
        var product = new Product(Guid.NewGuid());

        Assert.False(product.CanSubmitForReview(
            hasAtLeastOneImage: true,
            hasAtLeastOneActiveVariant: true));
        Assert.Throws<InvalidOperationException>(() => product.SubmitForReview(
            hasAtLeastOneImage: true,
            hasAtLeastOneActiveVariant: true));
    }

    [Fact]
    public void SubmitForReview_MovesDraftProductToPendingReview()
    {
        var product = CreateSubmittableProduct();

        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);

        Assert.Equal(ProductStatus.PendingReview, product.Status);
        Assert.Null(product.RejectionReason);
    }

    [Fact]
    public void Product_CannotBePublishedBySellerDirectly()
    {
        var product = CreateSubmittableProduct();

        Assert.Throws<InvalidOperationException>(() => product.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Publish_RequiresPendingReview()
    {
        var product = CreateSubmittableProduct();
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        var publishedAt = DateTimeOffset.UtcNow;

        product.Publish(publishedAt);

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(publishedAt, product.PublishedAtUtc);
    }

    [Fact]
    public void Reject_RequiresReasonAndAllowsSellerEdits()
    {
        var product = CreateSubmittableProduct();
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);

        Assert.Throws<ArgumentException>(() => product.Reject(" "));

        product.Reject("Images are not clear.");

        Assert.Equal(ProductStatus.Rejected, product.Status);
        Assert.Equal("Images are not clear.", product.RejectionReason);
        Assert.True(product.CanSellerEdit);
    }

    [Fact]
    public void RequestChanges_RequiresReasonAndAllowsSellerEdits()
    {
        var product = CreateSubmittableProduct();
        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);

        Assert.Throws<ArgumentException>(() => product.RequestChanges(" "));

        product.RequestChanges("Add clearer measurements.");

        Assert.Equal(ProductStatus.ChangesRequested, product.Status);
        Assert.Equal("Add clearer measurements.", product.RejectionReason);
        Assert.True(product.CanSellerEdit);
    }

    [Fact]
    public void MarkOutOfStockAndRestock_RequirePublishedStatus()
    {
        var product = CreateSubmittableProduct();
        Assert.Throws<InvalidOperationException>(() => product.MarkOutOfStock());

        product.SubmitForReview(hasAtLeastOneImage: true, hasAtLeastOneActiveVariant: true);
        product.Publish(DateTimeOffset.UtcNow);
        product.MarkOutOfStock();

        Assert.Equal(ProductStatus.OutOfStock, product.Status);

        product.Restock();

        Assert.Equal(ProductStatus.Published, product.Status);
    }

    [Fact]
    public void Slug_IsNormalizedAndValidated()
    {
        var product = new Product(Guid.NewGuid());
        product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Summer Dress",
            "Summer-Dress",
            "Short description",
            "Full description");

        Assert.Equal("summer-dress", product.Slug);
        Assert.Throws<ArgumentException>(() => product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Summer Dress",
            "bad slug",
            "Short description",
            "Full description"));
    }

    [Fact]
    public void MerchandisingAndSeoFields_AreTrimmedAndLengthValidated()
    {
        var product = new Product(Guid.NewGuid());

        product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Summer Dress",
            "summer-dress",
            "Short description",
            "Full description",
            "  SEO title  ",
            "  SEO description  ",
            "  Seller pick  ",
            "  Cold wash  ",
            "  Colour may vary  ");

        Assert.Equal("SEO title", product.SeoTitle);
        Assert.Equal("SEO description", product.SeoDescription);
        Assert.Equal("Seller pick", product.MerchandisingLabel);
        Assert.Equal("Cold wash", product.CareInstructions);
        Assert.Equal("Colour may vary", product.ProductDisclaimer);

        Assert.Throws<ArgumentException>(() => product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Summer Dress",
            "summer-dress",
            "Short description",
            "Full description",
            new string('x', Product.SeoTitleMaxLength + 1)));
    }

    private static Product CreateSubmittableProduct()
    {
        var product = new Product(Guid.NewGuid());
        product.UpdateDraftDetails(
            Guid.NewGuid(),
            null,
            "Summer Dress",
            "summer-dress",
            "A lightweight summer dress.",
            "A lightweight summer dress with a relaxed fit.");

        return product;
    }
}
