using System.Text.Json;
using Mabuntle.Application.Ai;

namespace Mabuntle.Infrastructure.Ai;

public sealed class FakeAiProviderClient : IAiProviderClient
{
    public Task<AiProviderResponse> GenerateListingSuggestionAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            suggestedTitle = "AI-assisted product title",
            suggestedShortDescription = "A concise marketplace-ready product summary.",
            suggestedFullDescription = "A draft listing description based only on seller-provided notes, known attributes, category hints, and image references.",
            suggestedCategoryId = (string?)null,
            suggestedCategoryPath = (string?)null,
            suggestedAttributes = new Dictionary<string, object?>(),
            suggestedTags = new[] { "draft", "ai-assisted" },
            missingFields = new[] { "brand", "material", "exact sizing" },
            riskFlags = Array.Empty<string>(),
            qualityScore = 65
        });

        return Task.FromResult(new AiProviderResponse(
            json,
            "local-fake-ai",
            InputTokenEstimate: request.Prompt.Length / 4,
            OutputTokenEstimate: json.Length / 4,
            CostEstimate: 0,
            LatencyMs: 1));
    }
}
