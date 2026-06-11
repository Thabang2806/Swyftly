namespace Mabuntle.Infrastructure.Storage;

public sealed class MediaScanningOptions
{
    public const string SectionName = "MediaScanning";

    public string ProviderName { get; set; } = TrustLocalCleanMediaMalwareScanner.ProviderName;

    public bool RequireExternalScannerInProduction { get; set; } = false;
}
