using Mabuntle.Domain.Catalog;

namespace Mabuntle.UnitTests.Domain;

public class CategoryTests
{
    [Fact]
    public void Category_NormalizesSlug()
    {
        var category = new Category(null, "Dresses", "Women-Clothing-Dresses", 10);

        Assert.Equal("women-clothing-dresses", category.Slug);
    }

    [Fact]
    public void Category_CannotBeItsOwnParent()
    {
        var categoryId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new Category(
            categoryId,
            categoryId,
            "Dresses",
            "dresses"));
    }

    [Fact]
    public void SelectAttribute_RequiresAllowedValues()
    {
        Assert.Throws<ArgumentException>(() => new CategoryAttribute(
            Guid.NewGuid(),
            "Size",
            "size",
            CategoryAttributeDataType.Select,
            isRequired: true));
    }

    [Fact]
    public void Validator_RequiresMandatoryCategoryAttributes()
    {
        var categoryId = Guid.NewGuid();
        var definitions = new[]
        {
            new CategoryAttribute(
                categoryId,
                "Size",
                "size",
                CategoryAttributeDataType.Select,
                isRequired: true,
                ["S", "M", "L"]),
            new CategoryAttribute(
                categoryId,
                "Colour",
                "colour",
                CategoryAttributeDataType.Text,
                isRequired: true)
        };

        var result = CategoryAttributeValidator.Validate(
            categoryId,
            definitions,
            new Dictionary<string, object?>
            {
                ["size"] = "M"
            });

        Assert.False(result.IsValid);
        Assert.Contains("Attribute 'colour' is required.", result.Errors);
    }

    [Fact]
    public void Validator_RejectsAttributesOutsideSelectedCategory()
    {
        var categoryId = Guid.NewGuid();
        var definitions = new[]
        {
            new CategoryAttribute(
                categoryId,
                "Size",
                "size",
                CategoryAttributeDataType.Select,
                isRequired: true,
                ["S", "M", "L"])
        };

        var result = CategoryAttributeValidator.Validate(
            categoryId,
            definitions,
            new Dictionary<string, object?>
            {
                ["size"] = "M",
                ["ring-size"] = "7"
            });

        Assert.False(result.IsValid);
        Assert.Contains("Attribute 'ring-size' is not valid for the selected category.", result.Errors);
    }

    [Fact]
    public void Validator_AcceptsRequiredAndAllowedValues()
    {
        var categoryId = Guid.NewGuid();
        var definitions = new[]
        {
            new CategoryAttribute(
                categoryId,
                "Skin Type",
                "skin-type",
                CategoryAttributeDataType.MultiSelect,
                isRequired: true,
                ["Dry", "Oily", "Combination"]),
            new CategoryAttribute(
                categoryId,
                "Volume ml",
                "volume-ml",
                CategoryAttributeDataType.Decimal,
                isRequired: false)
        };

        var result = CategoryAttributeValidator.Validate(
            categoryId,
            definitions,
            new Dictionary<string, object?>
            {
                ["skin-type"] = new[] { "Dry", "Combination" },
                ["volume-ml"] = "125.5"
            });

        Assert.True(result.IsValid);
    }
}
