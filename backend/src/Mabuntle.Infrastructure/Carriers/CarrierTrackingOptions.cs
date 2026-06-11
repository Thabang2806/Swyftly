namespace Mabuntle.Infrastructure.Carriers;

public sealed class CarrierTrackingOptions
{
    public const string SectionName = "CarrierTracking";

    public int BatchSize { get; set; } = 25;

    public int SyncIntervalMinutes { get; set; } = 15;
}
