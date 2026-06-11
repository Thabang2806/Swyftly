using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Ai;
using Mabuntle.Infrastructure.Ai;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class AiListingAssistantServiceTests
{
    [Fact]
    public async Task GenerateSuggestionAsync_WithValidProviderResponse_PersistsSuggestionAndUsageLog()
    {
        var now = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var timeProvider = new FixedTimeProvider(now);
        await using var dbContext = CreateDbContext();
        var categoryId = Guid.NewGuid();
        var provider = new StaticAiProviderClient(CreateValidJson(categoryId));
        var service = CreateService(dbContext, provider, timeProvider);

        var result = await service.GenerateSuggestionAsync(CreateRequest(categoryId, userId: "user-123"));

        Assert.True(result.IsSuccess);
        Assert.Equal("local-test-model", result.Value.ModelUsed);
        Assert.Equal("user-123", await dbContext.AiUsageLogs.Select(log => log.UserId).SingleAsync());

        var suggestion = await dbContext.AiProductSuggestions.SingleAsync();
        Assert.Equal(result.Value.SuggestionId, suggestion.Id);
        Assert.Equal("Black midi dress", suggestion.SuggestedTitle);
        Assert.Equal(now, suggestion.CreatedAtUtc);

        var usageLog = await dbContext.AiUsageLogs.SingleAsync();
        Assert.True(usageLog.Success);
        Assert.Null(usageLog.ErrorMessage);
    }

    [Fact]
    public async Task GenerateSuggestionAsync_WithInvalidProviderJson_ReturnsFailureAndLogsUsage()
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        await using var dbContext = CreateDbContext();
        var provider = new StaticAiProviderClient("{not valid json");
        var service = CreateService(dbContext, provider, timeProvider);

        var result = await service.GenerateSuggestionAsync(CreateRequest(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Empty(dbContext.AiProductSuggestions);

        var usageLog = await dbContext.AiUsageLogs.SingleAsync();
        Assert.False(usageLog.Success);
        Assert.Equal("local-test-model", usageLog.ModelUsed);
        Assert.NotNull(usageLog.ErrorMessage);
    }

    [Fact]
    public async Task GenerateSuggestionAsync_WhenProviderThrows_ReturnsFailureAndLogsUsage()
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        await using var dbContext = CreateDbContext();
        var provider = new ThrowingAiProviderClient();
        var service = CreateService(dbContext, provider, timeProvider);

        var result = await service.GenerateSuggestionAsync(CreateRequest(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Empty(dbContext.AiProductSuggestions);

        var usageLog = await dbContext.AiUsageLogs.SingleAsync();
        Assert.False(usageLog.Success);
        Assert.Equal("unknown", usageLog.ModelUsed);
        Assert.Equal("Provider unavailable.", usageLog.ErrorMessage);
    }

    private static AiListingAssistantService CreateService(
        MabuntleDbContext dbContext,
        IAiProviderClient provider,
        TimeProvider timeProvider) =>
        new(
            provider,
            new AiPromptBuilder(),
            new AiSuggestionValidator(),
            new AiUsageLogger(dbContext, timeProvider),
            dbContext,
            timeProvider);

    private static MabuntleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MabuntleDbContext(options);
    }

    private static AiListingAssistantRequest CreateRequest(Guid categoryId, string? userId = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Black dress with no confirmed brand.",
            "Dress",
            new Dictionary<string, object?> { ["condition"] = "New" },
            categoryId,
            [new AiListingImageReference(Guid.NewGuid(), "https://example.test/image.jpg", "Front image")],
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
            ],
            UserId: userId);

    private static string CreateValidJson(Guid categoryId) =>
        JsonSerializer.Serialize(new
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

    private sealed class StaticAiProviderClient(string json) : IAiProviderClient
    {
        public Task<AiProviderResponse> GenerateListingSuggestionAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiProviderResponse(
                json,
                "local-test-model",
                InputTokenEstimate: 100,
                OutputTokenEstimate: 50,
                CostEstimate: 0,
                LatencyMs: 5));
    }

    private sealed class ThrowingAiProviderClient : IAiProviderClient
    {
        public Task<AiProviderResponse> GenerateListingSuggestionAsync(
            AiProviderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Provider unavailable.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
