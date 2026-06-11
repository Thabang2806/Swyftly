using Mabuntle.Domain.Ai;

namespace Mabuntle.UnitTests.Domain;

public class AiUsageLogTests
{
    [Fact]
    public void UsageLog_CapturesSuccessfulUsage()
    {
        var log = new AiUsageLog(
            "ListingAssistant",
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            "gpt-test",
            inputTokenEstimate: 120,
            outputTokenEstimate: 80,
            costEstimate: 0.0125m,
            latencyMs: 350,
            success: true,
            errorMessage: null,
            DateTimeOffset.UtcNow);

        Assert.True(log.Success);
        Assert.Null(log.ErrorMessage);
        Assert.Equal(120, log.InputTokenEstimate);
        Assert.Equal(80, log.OutputTokenEstimate);
    }

    [Fact]
    public void UsageLog_CapturesFailureWithoutSecrets()
    {
        var log = new AiUsageLog(
            "ListingAssistant",
            Guid.NewGuid().ToString(),
            null,
            "gpt-test",
            inputTokenEstimate: null,
            outputTokenEstimate: null,
            costEstimate: null,
            latencyMs: 200,
            success: false,
            errorMessage: "Provider timeout.",
            DateTimeOffset.UtcNow);

        Assert.False(log.Success);
        Assert.Equal("Provider timeout.", log.ErrorMessage);
    }

    [Fact]
    public void UsageLog_RejectsNegativeEstimates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiUsageLog(
            "ListingAssistant",
            Guid.NewGuid().ToString(),
            null,
            "gpt-test",
            inputTokenEstimate: -1,
            outputTokenEstimate: null,
            costEstimate: null,
            latencyMs: 100,
            success: true,
            errorMessage: null,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PromptVersion_CanBeActivatedAndDeactivated()
    {
        var promptVersion = new AiPromptVersion(
            "ListingAssistant",
            "v1",
            "Suggest product listing fields.",
            isActive: true,
            DateTimeOffset.UtcNow);

        promptVersion.Deactivate();
        Assert.False(promptVersion.IsActive);

        promptVersion.Activate();
        Assert.True(promptVersion.IsActive);
    }
}
