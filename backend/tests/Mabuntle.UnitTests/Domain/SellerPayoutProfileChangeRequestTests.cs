using Mabuntle.Domain.Sellers;

namespace Mabuntle.UnitTests.Domain;

public sealed class SellerPayoutProfileChangeRequestTests
{
    [Fact]
    public void NewRequest_StartsAsDraft()
    {
        var sellerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var request = new SellerPayoutProfileChangeRequest(
            sellerId,
            "new-provider-ref",
            "Updated banking token.",
            requesterId);

        Assert.Equal(sellerId, request.SellerId);
        Assert.Equal(requesterId, request.RequestedByUserId);
        Assert.Equal(SellerPayoutProfileChangeRequestStatus.Draft, request.Status);
        Assert.True(request.IsActive);
    }

    [Fact]
    public void Submit_MovesDraftToPendingReview()
    {
        var request = CreateRequest();
        var submittedAt = DateTimeOffset.Parse("2026-05-21T10:00:00Z");

        request.Submit(submittedAt);

        Assert.Equal(SellerPayoutProfileChangeRequestStatus.PendingReview, request.Status);
        Assert.Equal(submittedAt, request.SubmittedAtUtc);
        Assert.True(request.IsActive);
    }

    [Fact]
    public void Approve_RejectsRequesterAsReviewer()
    {
        var requesterId = Guid.NewGuid();
        var request = CreateRequest(requesterId);
        request.Submit(DateTimeOffset.Parse("2026-05-21T10:00:00Z"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.Approve(requesterId, "Verified.", DateTimeOffset.Parse("2026-05-21T11:00:00Z")));

        Assert.Contains("requested", exception.Message);
    }

    [Fact]
    public void Reject_StoresReasonAndEndsActiveState()
    {
        var request = CreateRequest();
        request.Submit(DateTimeOffset.Parse("2026-05-21T10:00:00Z"));

        request.Reject(Guid.NewGuid(), "Could not verify reference.", DateTimeOffset.Parse("2026-05-21T11:00:00Z"));

        Assert.Equal(SellerPayoutProfileChangeRequestStatus.Rejected, request.Status);
        Assert.Equal("Could not verify reference.", request.ReviewReason);
        Assert.False(request.IsActive);
    }

    private static SellerPayoutProfileChangeRequest CreateRequest(Guid? requesterId = null) =>
        new(
            Guid.NewGuid(),
            "new-provider-ref",
            "Updated banking token.",
            requesterId ?? Guid.NewGuid());
}
