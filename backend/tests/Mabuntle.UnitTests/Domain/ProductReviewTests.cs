using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public sealed class ProductReviewTests
{
    [Fact]
    public void Constructor_StartsVerifiedPurchaseReviewPendingModeration()
    {
        var review = CreateReview(rating: 5);

        Assert.Equal(ProductReviewStatus.PendingReview, review.Status);
        Assert.False(review.IsPublic);
        Assert.Equal(5, review.Rating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_RejectsInvalidRating(int rating)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReview(rating));
    }

    [Fact]
    public void Approve_PublishesReview()
    {
        var review = CreateReview(rating: 4);
        var moderatorId = Guid.NewGuid();
        var moderatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

        review.Approve(moderatorId, moderatedAtUtc);

        Assert.Equal(ProductReviewStatus.Published, review.Status);
        Assert.True(review.IsPublic);
        Assert.Equal(moderatorId, review.ModeratedByUserId);
        Assert.Equal(moderatedAtUtc, review.ModeratedAtUtc);
        Assert.Null(review.ModerationReason);
    }

    [Fact]
    public void Reject_StoresModerationReason()
    {
        var review = CreateReview(rating: 4);
        var moderatorId = Guid.NewGuid();
        var moderatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

        review.Reject(" Needs clearer language. ", moderatorId, moderatedAtUtc);

        Assert.Equal(ProductReviewStatus.Rejected, review.Status);
        Assert.False(review.IsPublic);
        Assert.Equal("Needs clearer language.", review.ModerationReason);
        Assert.Equal(moderatorId, review.ModeratedByUserId);
        Assert.Equal(moderatedAtUtc, review.ModeratedAtUtc);
    }

    [Fact]
    public void Update_ChangesContentTimestampAndResetsToPendingReview()
    {
        var review = CreateReview(rating: 4);
        review.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1));
        var updatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

        review.Update(3, " Updated title ", " Updated body ", updatedAtUtc);

        Assert.Equal(3, review.Rating);
        Assert.Equal("Updated title", review.Title);
        Assert.Equal("Updated body", review.Body);
        Assert.Equal(ProductReviewStatus.PendingReview, review.Status);
        Assert.Null(review.ModerationReason);
        Assert.Null(review.ModeratedByUserId);
        Assert.Null(review.ModeratedAtUtc);
        Assert.Equal(updatedAtUtc, review.UpdatedAtUtc);
    }

    [Fact]
    public void Remove_HidesReviewAndPreventsFurtherUpdates()
    {
        var review = CreateReview(rating: 4);

        var moderatorId = Guid.NewGuid();
        var removedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

        review.Remove(removedAtUtc, moderatorId, "Policy violation.");

        Assert.Equal(ProductReviewStatus.Removed, review.Status);
        Assert.False(review.IsPublic);
        Assert.Equal("Policy violation.", review.ModerationReason);
        Assert.Equal(moderatorId, review.ModeratedByUserId);
        Assert.Equal(removedAtUtc, review.ModeratedAtUtc);
        Assert.Throws<InvalidOperationException>(() => review.Update(5, null, null, DateTimeOffset.UtcNow.AddMinutes(10)));
    }

    private static ProductReview CreateReview(int rating) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            rating,
            "Great fit",
            "The product matched the description.",
            DateTimeOffset.UtcNow);
}
