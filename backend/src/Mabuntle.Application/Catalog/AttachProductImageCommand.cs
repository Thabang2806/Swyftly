namespace Mabuntle.Application.Catalog;

public sealed record AttachProductImageCommand(
    string StorageKey,
    string? Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary);
