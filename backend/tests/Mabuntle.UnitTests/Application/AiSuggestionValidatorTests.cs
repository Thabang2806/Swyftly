using System.Text.Json;
using Mabuntle.Application.Ai;

namespace Mabuntle.UnitTests.Application;

public class AiSuggestionValidatorTests
{
    [Fact]
    public void Validate_WithValidProviderJson_ReturnsParsedSuggestion()
    {
        var categoryId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            suggestedTitle = "Black midi dress",
            suggestedShortDescription = "A black dress for seller review.",
            suggestedFullDescription = "Draft description based on provided details.",
            suggestedCategoryId = categoryId.ToString(),
            suggestedCategoryPath = "Women > Clothing > Dresses",
            suggestedAttributes = new Dictionary<string, object?> { ["colour"] = "Black" },
            suggestedTags = new[] { "dress" },
            missingFields = new[] { "brand" },
            riskFlags = Array.Empty<string>(),
            qualityScore = 72
        });

        var result = new AiSuggestionValidator().Validate(json, CreateRequest(categoryId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Black midi dress", result.Value.SuggestedTitle);
        Assert.Equal(categoryId, result.Value.SuggestedCategoryId);
        Assert.Equal("Black", result.Value.SuggestedAttributes["colour"]);
        Assert.Contains("brand", result.Value.MissingFields);
    }

    [Fact]
    public void Validate_WithInvalidJson_ReturnsFailure()
    {
        var result = new AiSuggestionValidator().Validate("{not valid json", CreateRequest(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Contains("json", result.Error.Details!.Keys);
    }

    [Fact]
    public void Validate_WithUnknownAttribute_ReturnsFailure()
    {
        var categoryId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            suggestedCategoryId = categoryId.ToString(),
            suggestedAttributes = new Dictionary<string, object?> { ["fabric"] = "Cotton" },
            suggestedTags = Array.Empty<string>(),
            missingFields = Array.Empty<string>(),
            riskFlags = Array.Empty<string>(),
            qualityScore = 50
        });

        var result = new AiSuggestionValidator().Validate(json, CreateRequest(categoryId));

        Assert.True(result.IsFailure);
        Assert.Contains("suggestedAttributes", result.Error.Details!.Keys);
    }

    [Fact]
    public void Validate_WithAttributeValueOutsideAllowedValues_ReturnsFailure()
    {
        var categoryId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            suggestedCategoryId = categoryId.ToString(),
            suggestedAttributes = new Dictionary<string, object?> { ["colour"] = "Blue" },
            suggestedTags = Array.Empty<string>(),
            missingFields = Array.Empty<string>(),
            riskFlags = Array.Empty<string>(),
            qualityScore = 50
        });

        var result = new AiSuggestionValidator().Validate(json, CreateRequest(categoryId));

        Assert.True(result.IsFailure);
        Assert.Contains("not allowed", result.Error.Details!["suggestedAttributes"].Single());
    }

    private static AiListingAssistantRequest CreateRequest(Guid categoryId) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Seller notes",
            "Dress",
            new Dictionary<string, object?>(),
            categoryId,
            [],
            [
                new AiListingCategoryReference(
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
                    ])
            ]);
}
