using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public class ProductListingRevisionTests
{
    [Fact]
    public void Revision_StartsAsDraft()
    {
        var revision = new ProductListingRevision(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ProductListingRevisionStatus.Draft, revision.Status);
        Assert.True(revision.CanSellerEdit);
    }

    [Fact]
    public void Revision_SubmitRequiresCompleteListingAndImage()
    {
        var revision = new ProductListingRevision(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => revision.SubmitForReview(hasAtLeastOneImage: false, DateTimeOffset.UtcNow));

        revision.UpdateProposal(
            Guid.NewGuid(),
            null,
            "Published dress refresh",
            "published-dress-refresh",
            "Short description",
            "Full description",
            "[]",
            "{}");

        revision.SubmitForReview(hasAtLeastOneImage: true, DateTimeOffset.UtcNow);

        Assert.Equal(ProductListingRevisionStatus.PendingReview, revision.Status);
        Assert.False(revision.CanSellerEdit);
    }

    [Fact]
    public void Revision_RejectStoresReasonAndEditResetsToDraft()
    {
        var revision = new ProductListingRevision(Guid.NewGuid(), Guid.NewGuid());
        revision.UpdateProposal(
            Guid.NewGuid(),
            null,
            "Published dress refresh",
            "published-dress-refresh",
            "Short description",
            "Full description",
            "[]",
            "{}");
        revision.SubmitForReview(hasAtLeastOneImage: true, DateTimeOffset.UtcNow);

        revision.Reject("Image evidence is unclear.", Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(ProductListingRevisionStatus.Rejected, revision.Status);
        Assert.Equal("Image evidence is unclear.", revision.RejectionReason);

        revision.UpdateProposal(
            Guid.NewGuid(),
            null,
            "Updated dress refresh",
            "updated-dress-refresh",
            "Short description",
            "Full description",
            "[]",
            "{}");

        Assert.Equal(ProductListingRevisionStatus.Draft, revision.Status);
        Assert.Null(revision.RejectionReason);
    }
}
