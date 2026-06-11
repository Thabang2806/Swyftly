using System.Text;
using System.Text.Json;

namespace Mabuntle.Application.Ai;

public sealed class AiPromptBuilder
{
    public string Build(AiListingAssistantRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are Mabuntle's AI Fashion Product Listing Assistant.");
        builder.AppendLine("Return structured JSON only. Do not include markdown or prose.");
        builder.AppendLine("Do not invent brand, material, authenticity, ingredients, expiry date, medical claims, or exact sizing.");
        builder.AppendLine("Add missing information to missingFields instead of inventing it.");
        builder.AppendLine("Flag beauty claims and counterfeit-risk wording in riskFlags.");
        builder.AppendLine("Use only the marketplace category list and allowed attributes supplied below.");
        builder.AppendLine();
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine("""
{
  "suggestedTitle": null,
  "suggestedShortDescription": null,
  "suggestedFullDescription": null,
  "suggestedCategoryId": null,
  "suggestedCategoryPath": null,
  "suggestedAttributes": {},
  "suggestedTags": [],
  "missingFields": [],
  "riskFlags": [],
  "qualityScore": 0
}
""");
        builder.AppendLine();
        builder.AppendLine("Seller notes:");
        builder.AppendLine(string.IsNullOrWhiteSpace(request.SellerNotes) ? "(none)" : request.SellerNotes.Trim());
        builder.AppendLine();
        builder.AppendLine("Product type hint:");
        builder.AppendLine(string.IsNullOrWhiteSpace(request.ProductTypeHint) ? "(none)" : request.ProductTypeHint.Trim());
        builder.AppendLine();
        builder.AppendLine("Known product attributes:");
        builder.AppendLine(JsonSerializer.Serialize(request.KnownAttributes));
        builder.AppendLine();
        builder.AppendLine("Category hint:");
        builder.AppendLine(request.CategoryHintId?.ToString() ?? "(none)");
        builder.AppendLine();
        builder.AppendLine("Image references:");
        builder.AppendLine(JsonSerializer.Serialize(request.ImageReferences));
        builder.AppendLine();
        builder.AppendLine("Marketplace categories and allowed attributes:");
        builder.AppendLine(JsonSerializer.Serialize(request.Categories));

        return builder.ToString();
    }
}
