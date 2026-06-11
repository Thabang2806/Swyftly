using Mabuntle.Domain.Sellers;

namespace Mabuntle.UnitTests.Domain;

public class SellerProfileTests
{
    [Fact]
    public void NewSellerProfile_StartsPendingVerification()
    {
        var seller = new SellerProfile(Guid.NewGuid());

        Assert.Equal(SellerVerificationStatus.PendingVerification, seller.VerificationStatus);
        Assert.False(seller.HasRequiredProfileFields());
    }

    [Fact]
    public void RegisteredBusiness_ProfileRequiresBusinessName()
    {
        var seller = new SellerProfile(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => seller.UpdateProfile(
            "Thabo Store",
            "seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            " "));
    }

    [Fact]
    public void SubmitForVerification_RequiresCompletedOnboardingFields()
    {
        var seller = CreateCompleteSeller();

        Assert.Throws<InvalidOperationException>(() => seller.SubmitForVerification(
            storefront: null,
            address: CreateAddress(seller.Id),
            payoutProfile: CreatePayoutProfile(seller.Id)));
    }

    [Fact]
    public void SubmitForVerification_MovesCompleteSellerUnderReview()
    {
        var seller = CreateCompleteSeller();
        var storefront = CreateStorefront(seller.Id);
        var address = CreateAddress(seller.Id);
        var payoutProfile = CreatePayoutProfile(seller.Id);

        seller.SubmitForVerification(storefront, address, payoutProfile);

        Assert.Equal(SellerVerificationStatus.UnderReview, seller.VerificationStatus);
    }

    [Fact]
    public void MarkVerified_RequiresAdminApprovedPayoutProfile()
    {
        var seller = CreateCompleteSeller();
        var storefront = CreateStorefront(seller.Id);
        var address = CreateAddress(seller.Id);
        var payoutProfile = CreatePayoutProfile(seller.Id);

        Assert.False(seller.CanBeVerified(storefront, address, payoutProfile));
        Assert.Throws<InvalidOperationException>(() => seller.MarkVerified(storefront, address, payoutProfile));
    }

    [Fact]
    public void MarkVerified_SucceedsWhenOnboardingAndPayoutAreApproved()
    {
        var seller = CreateCompleteSeller();
        var storefront = CreateStorefront(seller.Id);
        var address = CreateAddress(seller.Id);
        var payoutProfile = CreatePayoutProfile(seller.Id);
        payoutProfile.MarkAdminApproved(Guid.NewGuid(), DateTimeOffset.UtcNow);

        seller.MarkVerified(storefront, address, payoutProfile);

        Assert.Equal(SellerVerificationStatus.Verified, seller.VerificationStatus);
    }

    [Fact]
    public void Storefront_NormalizesSlugAndRejectsInvalidCharacters()
    {
        var sellerId = Guid.NewGuid();

        var storefront = new SellerStorefront(sellerId, "Thabo Store", "Thabo-Store");

        Assert.Equal("thabo-store", storefront.Slug);
        Assert.Throws<ArgumentException>(() => new SellerStorefront(sellerId, "Thabo Store", "bad slug"));
    }

    private static SellerProfile CreateCompleteSeller()
    {
        var seller = new SellerProfile(Guid.NewGuid());
        seller.UpdateProfile(
            "Thabo Store",
            "seller@example.test",
            "+27110000000",
            SellerBusinessType.RegisteredBusiness,
            "Thabo Trading");

        return seller;
    }

    private static SellerStorefront CreateStorefront(Guid sellerId) =>
        new(sellerId, "Thabo Store", "thabo-store");

    private static SellerAddress CreateAddress(Guid sellerId) =>
        new(
            sellerId,
            "1 Market Street",
            null,
            "Johannesburg",
            "Gauteng",
            "2000",
            "za");

    private static SellerPayoutProfilePlaceholder CreatePayoutProfile(Guid sellerId) =>
        new(sellerId, "provider-ref-123");
}
