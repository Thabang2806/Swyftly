using Mabuntle.Domain.Sellers;

namespace Mabuntle.UnitTests.Domain;

public sealed class SellerStorePolicyTests
{
    [Fact]
    public void Constructor_TrimsPolicyText()
    {
        var policy = new SellerStorePolicy(
            Guid.NewGuid(),
            14,
            " Returns accepted. ",
            " Exchanges reviewed. ",
            " Dispatch in two days. ",
            " Message support. ",
            " Cold wash. ",
            " Colours may vary. ");

        Assert.Equal("Returns accepted.", policy.ReturnPolicy);
        Assert.Equal("Exchanges reviewed.", policy.ExchangePolicy);
        Assert.Equal("Dispatch in two days.", policy.FulfilmentPolicy);
        Assert.Equal("Message support.", policy.SupportPolicy);
        Assert.Equal("Cold wash.", policy.CareInstructions);
        Assert.Equal("Colours may vary.", policy.ProductDisclaimer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Constructor_RejectsInvalidReturnWindow(int returnWindowDays)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SellerStorePolicy(
            Guid.NewGuid(),
            returnWindowDays,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    [Fact]
    public void Update_RejectsOverlongPolicyText()
    {
        var policy = new SellerStorePolicy(
            Guid.NewGuid(),
            14,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Throws<ArgumentException>(() => policy.Update(
            14,
            new string('x', SellerStorePolicy.ReturnPolicyMaxLength + 1),
            null,
            null,
            null,
            null,
            null));
    }
}
