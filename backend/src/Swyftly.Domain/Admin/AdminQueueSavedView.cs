using Swyftly.Domain.Common;

namespace Swyftly.Domain.Admin;

public sealed class AdminQueueSavedView : AuditableEntity
{
    public const int QueueMaxLength = 40;
    public const int NameMaxLength = 80;
    public const int ShortFilterMaxLength = 80;
    public const int SearchMaxLength = 200;
    public const int SortMaxLength = 40;

    private AdminQueueSavedView()
    {
    }

    public AdminQueueSavedView(
        Guid adminUserId,
        string queue,
        string name,
        AdminQueueSavedViewFilters filters,
        DateTimeOffset now)
    {
        AdminUserId = adminUserId;
        Queue = NormalizeRequired(queue, QueueMaxLength, nameof(queue));
        Name = NormalizeRequired(name, NameMaxLength, nameof(name));
        ApplyFilters(filters);
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid AdminUserId { get; private set; }

    public string Queue { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    public string? View { get; private set; }

    public string? Status { get; private set; }

    public string? Category { get; private set; }

    public string? Search { get; private set; }

    public Guid? SellerId { get; private set; }

    public string? Assigned { get; private set; }

    public string? Priority { get; private set; }

    public bool? HasNotes { get; private set; }

    public string? Sla { get; private set; }

    public string? Sort { get; private set; }

    public int PageSize { get; private set; } = 25;

    public void RenameAndUpdate(string name, AdminQueueSavedViewFilters filters, DateTimeOffset now)
    {
        Name = NormalizeRequired(name, NameMaxLength, nameof(name));
        ApplyFilters(filters);
        UpdatedAtUtc = now;
    }

    public void MarkDefault(DateTimeOffset now)
    {
        IsDefault = true;
        UpdatedAtUtc = now;
    }

    public void ClearDefault(DateTimeOffset now)
    {
        IsDefault = false;
        UpdatedAtUtc = now;
    }

    private void ApplyFilters(AdminQueueSavedViewFilters filters)
    {
        View = NormalizeOptional(filters.View, ShortFilterMaxLength);
        Status = NormalizeOptional(filters.Status, ShortFilterMaxLength);
        Category = NormalizeOptional(filters.Category, ShortFilterMaxLength);
        Search = NormalizeOptional(filters.Search, SearchMaxLength);
        SellerId = filters.SellerId;
        Assigned = NormalizeOptional(filters.Assigned, ShortFilterMaxLength);
        Priority = NormalizeOptional(filters.Priority, ShortFilterMaxLength);
        HasNotes = filters.HasNotes;
        Sla = NormalizeOptional(filters.Sla, ShortFilterMaxLength);
        Sort = NormalizeOptional(filters.Sort, SortMaxLength);
        PageSize = Math.Clamp(filters.PageSize ?? 25, 1, 100);
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} must be {maxLength} characters or fewer.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}

public sealed record AdminQueueSavedViewFilters(
    string? View,
    string? Status,
    string? Category,
    string? Search,
    Guid? SellerId,
    string? Assigned,
    string? Priority,
    bool? HasNotes,
    string? Sla,
    string? Sort,
    int? PageSize);
