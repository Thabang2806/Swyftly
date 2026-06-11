using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerVerificationEvidence : AuditableEntity
{
    public const int OriginalFileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StorageProviderMaxLength = 64;
    public const int StorageKeyMaxLength = 700;
    public const int Sha256HashMaxLength = 128;
    public const int NoteMaxLength = 500;

    private SellerVerificationEvidence()
    {
    }

    public SellerVerificationEvidence(
        Guid sellerId,
        SellerVerificationEvidenceType evidenceType,
        string storageProvider,
        string storageKey,
        string originalFileName,
        string contentType,
        long byteSize,
        string sha256Hash,
        string? note,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Uploaded by user id is required.", nameof(uploadedByUserId));
        }

        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size must be positive.");
        }

        SellerId = sellerId;
        EvidenceType = evidenceType;
        StorageProvider = Required(storageProvider, nameof(storageProvider), StorageProviderMaxLength);
        StorageKey = Required(storageKey, nameof(storageKey), StorageKeyMaxLength);
        OriginalFileName = Required(originalFileName, nameof(originalFileName), OriginalFileNameMaxLength);
        ContentType = Required(contentType, nameof(contentType), ContentTypeMaxLength).ToLowerInvariant();
        ByteSize = byteSize;
        Sha256Hash = Required(sha256Hash, nameof(sha256Hash), Sha256HashMaxLength);
        Note = TrimOrNull(note, NoteMaxLength);
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid SellerId { get; private set; }

    public SellerVerificationEvidenceType EvidenceType { get; private set; }

    public string StorageProvider { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long ByteSize { get; private set; }

    public string Sha256Hash { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public Guid? RemovedByUserId { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public bool IsActive => RemovedAtUtc is null;

    public void Remove(Guid removedByUserId, DateTimeOffset removedAtUtc)
    {
        if (removedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Removed by user id is required.", nameof(removedByUserId));
        }

        if (RemovedAtUtc.HasValue)
        {
            return;
        }

        RemovedByUserId = removedByUserId;
        RemovedAtUtc = removedAtUtc;
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }

        return trimmed;
    }
}
