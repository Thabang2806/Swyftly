using Mabuntle.Domain.Catalog;

namespace Mabuntle.Infrastructure.Persistence;

public static class CatalogSeedData
{
    public static readonly Guid Women = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid WomenClothing = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid WomenDresses = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid WomenTops = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid Men = Guid.Parse("20000000-0000-0000-0000-000000000005");
    public static readonly Guid MenClothing = Guid.Parse("20000000-0000-0000-0000-000000000006");
    public static readonly Guid MenShirts = Guid.Parse("20000000-0000-0000-0000-000000000007");
    public static readonly Guid Jewellery = Guid.Parse("20000000-0000-0000-0000-000000000008");
    public static readonly Guid JewelleryEarrings = Guid.Parse("20000000-0000-0000-0000-000000000009");
    public static readonly Guid JewelleryHoopEarrings = Guid.Parse("20000000-0000-0000-0000-000000000010");
    public static readonly Guid JewelleryRings = Guid.Parse("20000000-0000-0000-0000-000000000011");
    public static readonly Guid Accessories = Guid.Parse("20000000-0000-0000-0000-000000000012");
    public static readonly Guid AccessoriesBags = Guid.Parse("20000000-0000-0000-0000-000000000013");
    public static readonly Guid AccessoriesBelts = Guid.Parse("20000000-0000-0000-0000-000000000014");
    public static readonly Guid Beauty = Guid.Parse("20000000-0000-0000-0000-000000000015");
    public static readonly Guid BeautyMakeup = Guid.Parse("20000000-0000-0000-0000-000000000016");
    public static readonly Guid BeautyFoundation = Guid.Parse("20000000-0000-0000-0000-000000000017");
    public static readonly Guid BeautySkincare = Guid.Parse("20000000-0000-0000-0000-000000000018");
    public static readonly Guid BeautyCleansers = Guid.Parse("20000000-0000-0000-0000-000000000019");

    public static IReadOnlyCollection<Category> CreateCategories() =>
    [
        new(Women, null, "Women", "women", 10),
        new(WomenClothing, Women, "Clothing", "women-clothing", 10),
        new(WomenDresses, WomenClothing, "Dresses", "women-clothing-dresses", 10),
        new(WomenTops, WomenClothing, "Tops", "women-clothing-tops", 20),
        new(Men, null, "Men", "men", 20),
        new(MenClothing, Men, "Clothing", "men-clothing", 10),
        new(MenShirts, MenClothing, "Shirts", "men-clothing-shirts", 10),
        new(Jewellery, null, "Jewellery", "jewellery", 30),
        new(JewelleryEarrings, Jewellery, "Earrings", "jewellery-earrings", 10),
        new(JewelleryHoopEarrings, JewelleryEarrings, "Hoop Earrings", "jewellery-earrings-hoop-earrings", 10),
        new(JewelleryRings, Jewellery, "Rings", "jewellery-rings", 20),
        new(Accessories, null, "Accessories", "accessories", 40),
        new(AccessoriesBags, Accessories, "Bags", "accessories-bags", 10),
        new(AccessoriesBelts, Accessories, "Belts", "accessories-belts", 20),
        new(Beauty, null, "Beauty", "beauty", 50),
        new(BeautyMakeup, Beauty, "Makeup", "beauty-makeup", 10),
        new(BeautyFoundation, BeautyMakeup, "Foundation", "beauty-makeup-foundation", 10),
        new(BeautySkincare, Beauty, "Skincare", "beauty-skincare", 20),
        new(BeautyCleansers, BeautySkincare, "Cleansers", "beauty-skincare-cleansers", 10)
    ];

    public static IReadOnlyCollection<CategoryAttribute> CreateCategoryAttributes() =>
    [
        Select("30000000-0000-0000-0000-000000000001", WomenDresses, "Size", "size", true, 10, "XS", "S", "M", "L", "XL"),
        Text("30000000-0000-0000-0000-000000000002", WomenDresses, "Colour", "colour", true, 20),
        Text("30000000-0000-0000-0000-000000000003", WomenDresses, "Material", "material", false, 30),
        Select("30000000-0000-0000-0000-000000000004", WomenTops, "Size", "size", true, 10, "XS", "S", "M", "L", "XL"),
        Text("30000000-0000-0000-0000-000000000005", WomenTops, "Colour", "colour", true, 20),
        Select("30000000-0000-0000-0000-000000000006", MenShirts, "Size", "size", true, 10, "S", "M", "L", "XL", "XXL"),
        Text("30000000-0000-0000-0000-000000000007", MenShirts, "Colour", "colour", true, 20),
        Text("30000000-0000-0000-0000-000000000008", MenShirts, "Fit", "fit", false, 30),
        Select("30000000-0000-0000-0000-000000000009", JewelleryHoopEarrings, "Material", "material", true, 10, "Gold", "Silver", "Stainless Steel", "Beaded"),
        Text("30000000-0000-0000-0000-000000000010", JewelleryHoopEarrings, "Colour", "colour", false, 20),
        Select("30000000-0000-0000-0000-000000000011", JewelleryRings, "Material", "material", true, 10, "Gold", "Silver", "Stainless Steel"),
        Select("30000000-0000-0000-0000-000000000012", JewelleryRings, "Ring Size", "ring-size", true, 20, "5", "6", "7", "8", "9", "10"),
        Select("30000000-0000-0000-0000-000000000013", AccessoriesBags, "Material", "material", true, 10, "Leather", "Vegan Leather", "Canvas", "Fabric"),
        Text("30000000-0000-0000-0000-000000000014", AccessoriesBags, "Colour", "colour", true, 20),
        Select("30000000-0000-0000-0000-000000000015", AccessoriesBelts, "Size", "size", true, 10, "S", "M", "L", "XL"),
        Select("30000000-0000-0000-0000-000000000016", AccessoriesBelts, "Material", "material", true, 20, "Leather", "Vegan Leather", "Fabric"),
        Text("30000000-0000-0000-0000-000000000017", BeautyFoundation, "Shade", "shade", true, 10),
        MultiSelect("30000000-0000-0000-0000-000000000018", BeautyFoundation, "Skin Type", "skin-type", false, 20, "Dry", "Oily", "Combination", "Sensitive"),
        Decimal("30000000-0000-0000-0000-000000000019", BeautyFoundation, "Volume ml", "volume-ml", false, 30),
        MultiSelect("30000000-0000-0000-0000-000000000020", BeautyCleansers, "Skin Type", "skin-type", true, 10, "Dry", "Oily", "Combination", "Sensitive"),
        Decimal("30000000-0000-0000-0000-000000000021", BeautyCleansers, "Volume ml", "volume-ml", false, 20)
    ];

    private static CategoryAttribute Text(
        string id,
        Guid categoryId,
        string name,
        string key,
        bool isRequired,
        int displayOrder) =>
        new(Guid.Parse(id), categoryId, name, key, CategoryAttributeDataType.Text, isRequired, displayOrder: displayOrder);

    private static CategoryAttribute Decimal(
        string id,
        Guid categoryId,
        string name,
        string key,
        bool isRequired,
        int displayOrder) =>
        new(Guid.Parse(id), categoryId, name, key, CategoryAttributeDataType.Decimal, isRequired, displayOrder: displayOrder);

    private static CategoryAttribute Select(
        string id,
        Guid categoryId,
        string name,
        string key,
        bool isRequired,
        int displayOrder,
        params string[] allowedValues) =>
        new(Guid.Parse(id), categoryId, name, key, CategoryAttributeDataType.Select, isRequired, allowedValues, displayOrder);

    private static CategoryAttribute MultiSelect(
        string id,
        Guid categoryId,
        string name,
        string key,
        bool isRequired,
        int displayOrder,
        params string[] allowedValues) =>
        new(Guid.Parse(id), categoryId, name, key, CategoryAttributeDataType.MultiSelect, isRequired, allowedValues, displayOrder);
}
