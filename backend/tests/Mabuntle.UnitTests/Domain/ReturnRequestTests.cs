using Mabuntle.Domain.Returns;

namespace Mabuntle.UnitTests.Domain;

public class ReturnRequestTests
{
    [Fact]
    public void Constructor_StartsRequested()
    {
        var requestedAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var returnRequest = new ReturnRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReturnReason.WrongSize,
            "Too small.",
            requestedAt);

        Assert.Equal(ReturnStatus.Requested, returnRequest.Status);
        Assert.Equal(ReturnReason.WrongSize, returnRequest.Reason);
        Assert.Equal(requestedAt, returnRequest.RequestedAtUtc);
    }

    [Fact]
    public void AddItem_PreventsDuplicateOrderItem()
    {
        var returnRequest = new ReturnRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReturnReason.DamagedItem,
            null,
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        var orderItemId = Guid.NewGuid();

        returnRequest.AddItem(orderItemId, Guid.NewGuid(), Guid.NewGuid(), 1, ReturnReason.WrongItem, false, null);

        Assert.Throws<InvalidOperationException>(() =>
            returnRequest.AddItem(orderItemId, Guid.NewGuid(), Guid.NewGuid(), 1, ReturnReason.WrongItem, false, null));
    }

    [Fact]
    public void Approve_RequiresAwaitingSellerResponse()
    {
        var returnRequest = CreateReturnRequest();

        returnRequest.Approve(Guid.NewGuid(), "Accepted.", DateTimeOffset.UtcNow);

        Assert.Equal(ReturnStatus.Approved, returnRequest.Status);
        Assert.Equal("Accepted.", returnRequest.SellerResponseReason);
        Assert.Single(returnRequest.Messages);
    }

    [Fact]
    public void Dispute_RequiresRejectedReturn()
    {
        var returnRequest = CreateReturnRequest();

        Assert.Throws<InvalidOperationException>(() =>
            returnRequest.Dispute(Guid.NewGuid(), "Please review.", DateTimeOffset.UtcNow));
    }

    private static ReturnRequest CreateReturnRequest()
    {
        var returnRequest = new ReturnRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReturnReason.DamagedItem,
            null,
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        returnRequest.MarkAwaitingSellerResponse(DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        return returnRequest;
    }
}
