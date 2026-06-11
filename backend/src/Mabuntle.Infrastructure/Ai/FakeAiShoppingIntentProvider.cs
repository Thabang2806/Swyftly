using System.Globalization;
using System.Text.RegularExpressions;
using Mabuntle.Application.Ai;

namespace Mabuntle.Infrastructure.Ai;

public sealed partial class FakeAiShoppingIntentProvider : IAiShoppingIntentProvider
{
    public Task<ShoppingIntent> ExtractIntentAsync(
        ShoppingIntentExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.BuyerMessage.Trim();
        var normalized = message.ToLowerInvariant();
        var category = FirstMatch(normalized, [
            ("dress", "Dresses"),
            ("dresses", "Dresses"),
            ("earring", "Jewellery"),
            ("earrings", "Jewellery"),
            ("skincare", "Beauty"),
            ("skin care", "Beauty"),
            ("shoes", "Shoes"),
            ("sneakers", "Shoes")
        ]);
        var subcategory = FirstMatch(normalized, [
            ("earring", "Earrings"),
            ("earrings", "Earrings"),
            ("skincare", "Skincare"),
            ("skin care", "Skincare")
        ]);
        var colour = FirstMatch(normalized, [
            ("black", "Black"),
            ("white", "White"),
            ("gold", "Gold"),
            ("silver", "Silver"),
            ("red", "Red"),
            ("blue", "Blue")
        ]);
        var size = FirstMatch(normalized, [
            ("size medium", "M"),
            ("medium", "M"),
            ("size small", "S"),
            ("small", "S"),
            ("size large", "L"),
            ("large", "L")
        ]);
        var occasion = FirstMatch(normalized, [
            ("wedding", "Wedding"),
            ("work", "Work"),
            ("office", "Work"),
            ("party", "Party")
        ]);
        var style = FirstMatch(normalized, [
            ("formal", "Formal"),
            ("casual", "Casual"),
            ("minimal", "Minimal"),
            ("streetwear", "Streetwear")
        ]);
        var material = FirstMatch(normalized, [
            ("gold", "Gold"),
            ("cotton", "Cotton"),
            ("leather", "Leather"),
            ("linen", "Linen")
        ]);
        var beautySkinType = FirstMatch(normalized, [
            ("oily skin", "Oily"),
            ("dry skin", "Dry"),
            ("sensitive skin", "Sensitive")
        ]);
        var beautyConcern = FirstMatch(normalized, [
            ("sensitive ears", "Sensitive ears"),
            ("acne", "Acne"),
            ("dark spots", "Dark spots"),
            ("hydration", "Hydration")
        ]);
        var budgetMax = ReadBudgetMax(normalized);

        var isVague = category is null
            && budgetMax is null
            && size is null
            && colour is null
            && occasion is null
            && style is null
            && material is null
            && beautySkinType is null
            && beautyConcern is null;

        return Task.FromResult(new ShoppingIntent(
            category,
            subcategory,
            budgetMax,
            BudgetMin: null,
            size,
            colour,
            occasion,
            style,
            material,
            Brand: null,
            beautySkinType,
            beautyConcern,
            message,
            isVague,
            isVague ? "Please add a product type, occasion, colour, size, or budget." : null));
    }

    private static string? FirstMatch(string text, IReadOnlyCollection<(string Term, string Value)> matches) =>
        matches.FirstOrDefault(match => text.Contains(match.Term, StringComparison.OrdinalIgnoreCase)).Value;

    private static decimal? ReadBudgetMax(string text)
    {
        var match = BudgetMaxRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["amount"].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    [GeneratedRegex(@"(?:under|below|less than|max|maximum)\s*r?\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex BudgetMaxRegex();
}
