using Mabuntle.Application.Ai;

namespace Mabuntle.UnitTests.Application;

public class AiPromptBuilderTests
{
    [Fact]
    public void Build_IncludesStructuredJsonShapeAndSafetyRules()
    {
        var categoryId = Guid.NewGuid();
        var prompt = new AiPromptBuilder().Build(new AiListingAssistantRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Black cotton dress. Seller is not sure about brand.",
            "Midi dress",
            new Dictionary<string, object?> { ["condition"] = "New" },
            categoryId,
            [new AiListingImageReference(Guid.NewGuid(), "https://example.test/image.jpg", "Front image")],
            [CreateCategory(categoryId)]));

        Assert.Contains("Return structured JSON only", prompt);
        Assert.Contains("Do not invent brand, material, authenticity, ingredients, expiry date, medical claims, or exact sizing.", prompt);
        Assert.Contains("missingFields", prompt);
        Assert.Contains("riskFlags", prompt);
        Assert.Contains(categoryId.ToString(), prompt);
        Assert.Contains("colour", prompt);
    }

    private static AiListingCategoryReference CreateCategory(Guid categoryId) =>
        new(
            categoryId,
            "Dresses",
            "dresses",
            "Women > Clothing > Dresses",
            [
                new AiListingCategoryAttributeReference(
                    "colour",
                    "Colour",
                    "select",
                    IsRequired: true,
                    ["Black", "White"])
            ]);
}
