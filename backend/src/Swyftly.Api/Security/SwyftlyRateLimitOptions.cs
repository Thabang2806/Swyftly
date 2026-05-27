namespace Swyftly.Api.Security;

public sealed class SwyftlyRateLimitOptions
{
    public const string SectionName = "SwyftlyRateLimits";

    public RateLimitPolicyOptions Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitPolicyOptions Ai { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public RateLimitPolicyOptions ProductWrite { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    public RateLimitPolicyOptions Payment { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitPolicyOptions Webhook { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public RateLimitPolicyOptions AdImpression { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public RateLimitPolicyOptions AdClick { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public RateLimitPolicyOptions StorefrontAnalytics { get; set; } = new() { PermitLimit = 180, WindowSeconds = 60 };

    public RateLimitPolicyOptions Search { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    private int _permitLimit = 1;
    private int _windowSeconds = 1;

    public int PermitLimit
    {
        get => _permitLimit;
        set => _permitLimit = Math.Max(1, value);
    }

    public int WindowSeconds
    {
        get => _windowSeconds;
        set => _windowSeconds = Math.Max(1, value);
    }
}
