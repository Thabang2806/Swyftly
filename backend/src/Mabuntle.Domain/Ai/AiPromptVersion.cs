using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Ai;

public sealed class AiPromptVersion : Entity
{
    private AiPromptVersion()
    {
    }

    public AiPromptVersion(
        string featureName,
        string version,
        string promptTemplate,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        FeatureName = Required(featureName, nameof(featureName));
        Version = Required(version, nameof(version));
        PromptTemplate = Required(promptTemplate, nameof(promptTemplate));
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public string FeatureName { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string PromptTemplate { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }
}
