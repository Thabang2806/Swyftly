namespace Mabuntle.Application.Sellers;

public sealed record SellerVerificationEvidenceResponse(
    Guid EvidenceId,
    string EvidenceType,
    string OriginalFileName,
    string ContentType,
    long ByteSize,
    string Sha256Hash,
    string? Note,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset? RemovedAtUtc);

public sealed record SellerVerificationEvidenceStorageRequest(
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    Guid SellerId);

public sealed record SellerVerificationEvidenceStoredFile(
    string StorageProvider,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long ByteSize,
    string Sha256Hash);

public sealed record SellerVerificationEvidenceReadFile(
    Stream Content,
    string ContentType,
    string FileName,
    long Length) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface ISellerVerificationEvidenceStorage
{
    Task<SellerVerificationEvidenceStoredFile> StoreAsync(
        SellerVerificationEvidenceStorageRequest request,
        CancellationToken cancellationToken = default);

    Task<SellerVerificationEvidenceReadFile?> OpenReadAsync(
        string storageKey,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
