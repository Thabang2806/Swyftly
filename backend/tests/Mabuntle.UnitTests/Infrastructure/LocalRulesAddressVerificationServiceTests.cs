using Mabuntle.Application.Delivery;
using Mabuntle.Domain.Delivery;
using Mabuntle.Infrastructure.Delivery;

namespace Mabuntle.UnitTests.Infrastructure;

public class LocalRulesAddressVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_NormalizesSouthAfricanProvinceAndPostalCode()
    {
        var service = new LocalRulesAddressVerificationService(TimeProvider.System);

        var result = await service.VerifyAsync(new AddressVerificationRequest(
            "Thabo Buyer ",
            "+27110000000",
            " 10 Market Street ",
            null,
            "Rosebank",
            "Johannesburg",
            " gp ",
            "2196",
            "za"));

        Assert.Equal(AddressVerificationStatus.Verified, result.Status);
        Assert.Equal("Gauteng", result.Province);
        Assert.Equal("ZA", result.CountryCode);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNeedsReviewForSuspiciousSouthAfricanAddress()
    {
        var service = new LocalRulesAddressVerificationService(TimeProvider.System);

        var result = await service.VerifyAsync(new AddressVerificationRequest(
            "Thabo Buyer",
            "+27110000000",
            "10 Market Street",
            null,
            null,
            "Johannesburg",
            "Unknown Province",
            "ABC",
            "ZA"));

        Assert.Equal(AddressVerificationStatus.NeedsReview, result.Status);
        Assert.Contains(result.Warnings, warning => warning.Contains("Province", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, warning => warning.Contains("postal codes", StringComparison.OrdinalIgnoreCase));
    }
}
