namespace Mabuntle.Application.Abstractions;

public interface IImageStorageProvider
{
    Task<ImageStorageReference> CreateReferenceAsync(
        CreateImageReferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<ImageStorageReference> UploadAsync(
        UploadImageStorageRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<ImageStorageReadinessResult> CheckReadinessAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CreateImageReferenceRequest(
    string StorageKey,
    string? Url);

public sealed record UploadImageStorageRequest(
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string Scope);

public sealed record ImageStorageReference(
    string StorageKey,
    string Url);

public sealed record ImageStorageReadinessResult(
    bool IsReady,
    string Description,
    IReadOnlyDictionary<string, string> Failures)
{
    public static ImageStorageReadinessResult Ready(string description) =>
        new(true, description, new Dictionary<string, string>());

    public static ImageStorageReadinessResult NotReady(
        string description,
        IReadOnlyDictionary<string, string> failures) =>
        new(false, description, failures);
}
