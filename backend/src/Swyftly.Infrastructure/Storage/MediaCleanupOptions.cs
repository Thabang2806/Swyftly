namespace Swyftly.Infrastructure.Storage;

public sealed class MediaCleanupOptions
{
    public const string SectionName = "MediaCleanup";

    public int GracePeriodHours { get; set; } = 24;

    public int BatchSize { get; set; } = 50;
}
