using Mabuntle.Application.Catalog;
using Mabuntle.Domain.Ai;

namespace Mabuntle.UnitTests.Application;

public class ProductModerationServiceTests
{
    [Fact]
    public void Moderate_WithCounterfeitRiskTerms_ReturnsHighRisk()
    {
        var decision = new ProductModerationService().Moderate(CreateRequest(
            title: "Designer inspired handbag",
            fullDescription: "Replica mirror quality finish."));

        Assert.True(decision.NeedsAdminReview);
        Assert.Equal(AiModerationRiskLevel.High, decision.RiskLevel);
        Assert.Contains("designer inspired", decision.DetectedTerms);
        Assert.Contains("replica", decision.DetectedTerms);
        Assert.Contains(decision.Flags, flag => flag.Code == "CounterfeitRisk");
    }

    [Fact]
    public void Moderate_WithBeautyClaims_ReturnsHighRisk()
    {
        var decision = new ProductModerationService().Moderate(CreateRequest(
            categoryPath: "Beauty > Skincare > Cleansers",
            fullDescription: "Clinically proven cleanser that cures acne."));

        Assert.True(decision.NeedsAdminReview);
        Assert.Contains("clinically proven", decision.DetectedTerms);
        Assert.Contains("cures acne", decision.DetectedTerms);
        Assert.Contains(decision.Flags, flag => flag.Code == "BeautyClaimRisk");
    }

    [Fact]
    public void Moderate_WithBeautyMissingFields_ReturnsMissingFieldFlags()
    {
        var decision = new ProductModerationService().Moderate(CreateRequest(
            categoryPath: "Beauty > Makeup > Foundation",
            attributes: new Dictionary<string, object?> { ["shade"] = "Medium" }));

        Assert.True(decision.NeedsAdminReview);
        Assert.Contains("ingredients", decision.MissingFields);
        Assert.Contains("expiry date", decision.MissingFields);
        Assert.Contains("batch number", decision.MissingFields);
        Assert.Contains("sealed/unsealed status", decision.MissingFields);
        Assert.Contains(decision.Flags, flag => flag.Code == "BeautyMissingFields");
    }

    [Fact]
    public void Moderate_WithNoRisk_ReturnsLowRisk()
    {
        var decision = new ProductModerationService().Moderate(CreateRequest());

        Assert.False(decision.NeedsAdminReview);
        Assert.Equal(AiModerationRiskLevel.Low, decision.RiskLevel);
        Assert.Empty(decision.Flags);
    }

    private static ProductModerationRequest CreateRequest(
        string? categoryPath = "Women > Clothing > Dresses",
        string? title = "Summer dress",
        string? fullDescription = "A lightweight summer dress.",
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            categoryPath,
            title,
            "Lightweight dress.",
            fullDescription,
            attributes ?? new Dictionary<string, object?> { ["size"] = "M", ["colour"] = "Black" },
            ["summer"],
            []);
}
