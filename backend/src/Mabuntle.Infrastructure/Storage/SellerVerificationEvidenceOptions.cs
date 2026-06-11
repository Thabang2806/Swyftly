namespace Mabuntle.Infrastructure.Storage;

public sealed class SellerVerificationEvidenceOptions
{
    public const string SectionName = "SellerVerificationEvidence";

    public string LocalRootPath { get; set; } = "storage/seller-verification-evidence";

    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxActiveFilesPerSeller { get; set; } = 20;

    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    ];
}
