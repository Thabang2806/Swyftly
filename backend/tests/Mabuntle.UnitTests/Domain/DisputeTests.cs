using Mabuntle.Domain.Disputes;

namespace Mabuntle.UnitTests.Domain;

public class DisputeTests
{
    [Fact]
    public void Constructor_RecordsBuyerOpeningMessage()
    {
        var openedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var dispute = new Dispute(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Order was not as described.",
            openedAt);

        Assert.Equal(DisputeStatus.AwaitingSeller, dispute.Status);
        Assert.True(dispute.IsActive);
        var message = Assert.Single(dispute.Messages);
        Assert.Equal("Buyer", message.SenderRole);
    }

    [Fact]
    public void AddEvidence_RecordsEvidenceReference()
    {
        var dispute = CreateDispute();

        dispute.AddEvidence(Guid.NewGuid(), "Buyer", "Image", "uploads/disputes/photo.jpg", "Photo evidence.", DateTimeOffset.UtcNow);

        var evidence = Assert.Single(dispute.Evidence);
        Assert.Equal("Image", evidence.EvidenceType);
        Assert.Equal("uploads/disputes/photo.jpg", evidence.StorageReference);
    }

    [Fact]
    public void ResolveSellerFavoured_ClosesActiveDispute()
    {
        var dispute = CreateDispute();

        dispute.ResolveSellerFavoured(Guid.NewGuid(), "Seller evidence accepted.", DateTimeOffset.UtcNow);

        Assert.Equal(DisputeStatus.ResolvedSellerFavoured, dispute.Status);
        Assert.False(dispute.IsActive);
        Assert.Equal("Seller evidence accepted.", dispute.ResolutionReason);
    }

    [Fact]
    public void ResolvedDispute_CannotBeChanged()
    {
        var dispute = CreateDispute();
        dispute.ResolveBuyerFavoured(Guid.NewGuid(), "Buyer evidence accepted.", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            dispute.AddMessage(Guid.NewGuid(), "Seller", "Late response.", DateTimeOffset.UtcNow));
    }

    private static Dispute CreateDispute() =>
        new(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Order was not as described.",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
}
