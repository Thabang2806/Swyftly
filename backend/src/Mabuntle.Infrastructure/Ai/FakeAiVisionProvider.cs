using Mabuntle.Application.Ai;

namespace Mabuntle.Infrastructure.Ai;

public sealed class FakeAiVisionProvider : IAiVisionProvider
{
    public Task<VisualSearchAttributes> ExtractAttributesAsync(
        VisualSearchExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = string.Join(
                ' ',
                request.ImageReference,
                request.FileName,
                request.ContentType)
            .Trim();
        var normalized = source.ToLowerInvariant();

        var category = FirstMatch(normalized, [
            ("dress", "Dresses"),
            ("gown", "Dresses"),
            ("earring", "Jewellery"),
            ("ring", "Jewellery"),
            ("shoe", "Shoes"),
            ("sneaker", "Shoes"),
            ("serum", "Beauty"),
            ("skincare", "Beauty")
        ]);
        var colour = FirstMatch(normalized, [
            ("black", "Black"),
            ("white", "White"),
            ("red", "Red"),
            ("blue", "Blue"),
            ("green", "Green"),
            ("gold", "Gold"),
            ("silver", "Silver"),
            ("pink", "Pink")
        ]);
        var style = FirstMatch(normalized, [
            ("formal", "Formal"),
            ("casual", "Casual"),
            ("minimal", "Minimal"),
            ("streetwear", "Streetwear"),
            ("wedding", "Formal")
        ]);
        var shape = FirstMatch(normalized, [
            ("maxi", "Maxi"),
            ("mini", "Mini"),
            ("hoop", "Hoop"),
            ("stud", "Stud"),
            ("sneaker", "Low-top")
        ]);
        var pattern = FirstMatch(normalized, [
            ("stripe", "Striped"),
            ("floral", "Floral"),
            ("plain", "Plain"),
            ("solid", "Plain")
        ]);
        var material = FirstMatch(normalized, [
            ("cotton", "Cotton"),
            ("linen", "Linen"),
            ("leather", "Leather"),
            ("silk", "Silk")
        ]);

        var terms = new[] { category, colour, style, shape, pattern, material }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warnings = material is null
            ? new[] { "Material and brand are not inferred unless visible context is explicit." }
            : new[] { "Material is a low-confidence visual guess and should not be treated as verified." };

        return Task.FromResult(new VisualSearchAttributes(
            category,
            colour,
            style,
            shape,
            pattern,
            material,
            material is null ? null : 0.45m,
            terms.Length == 0 ? 0.2m : 0.72m,
            string.Join(' ', terms),
            warnings));
    }

    private static string? FirstMatch(string text, IReadOnlyCollection<(string Term, string Value)> matches) =>
        matches.FirstOrDefault(match => text.Contains(match.Term, StringComparison.OrdinalIgnoreCase)).Value;
}
