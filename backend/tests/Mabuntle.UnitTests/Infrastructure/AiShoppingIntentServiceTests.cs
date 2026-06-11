using Mabuntle.Application.Ai;
using Mabuntle.Infrastructure.Ai;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class AiShoppingIntentServiceTests
{
    [Fact]
    public async Task ExtractIntentAsync_ExtractsFashionIntentWithBudget()
    {
        var service = CreateService();

        var result = await service.ExtractIntentAsync(new ShoppingIntentExtractionRequest(
            "I need a black dress in size medium for a wedding under R1,500."));

        Assert.True(result.IsSuccess);
        Assert.Equal("Dresses", result.Value.Category);
        Assert.Equal("Black", result.Value.Colour);
        Assert.Equal("M", result.Value.Size);
        Assert.Equal("Wedding", result.Value.Occasion);
        Assert.Equal(1500m, result.Value.BudgetMax);
        Assert.False(result.Value.IsVague);
    }

    [Fact]
    public async Task ExtractIntentAsync_ExtractsBeautyAndJewelleryConcerns()
    {
        var service = CreateService();

        var jewellery = await service.ExtractIntentAsync(new ShoppingIntentExtractionRequest(
            "Find gold earrings for sensitive ears."));
        var beauty = await service.ExtractIntentAsync(new ShoppingIntentExtractionRequest(
            "I need skincare for oily skin under R300."));

        Assert.True(jewellery.IsSuccess);
        Assert.Equal("Jewellery", jewellery.Value.Category);
        Assert.Equal("Earrings", jewellery.Value.Subcategory);
        Assert.Equal("Gold", jewellery.Value.Material);
        Assert.Equal("Sensitive ears", jewellery.Value.BeautyConcern);

        Assert.True(beauty.IsSuccess);
        Assert.Equal("Beauty", beauty.Value.Category);
        Assert.Equal("Skincare", beauty.Value.Subcategory);
        Assert.Equal("Oily", beauty.Value.BeautySkinType);
        Assert.Equal(300m, beauty.Value.BudgetMax);
    }

    [Fact]
    public async Task ExtractIntentAsync_ReturnsValidationForBlankMessage()
    {
        var service = CreateService();

        var result = await service.ExtractIntentAsync(new ShoppingIntentExtractionRequest(" "));

        Assert.True(result.IsFailure);
        Assert.Equal("Validation", result.Error.Type.ToString());
    }

    [Fact]
    public async Task ExtractIntentAsync_HandlesVagueIntentGracefully()
    {
        var service = CreateService();

        var result = await service.ExtractIntentAsync(new ShoppingIntentExtractionRequest("Help me shop."));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsVague);
        Assert.NotNull(result.Value.ClarificationPrompt);
    }

    private static AiShoppingIntentService CreateService() =>
        new(new FakeAiShoppingIntentProvider());
}
