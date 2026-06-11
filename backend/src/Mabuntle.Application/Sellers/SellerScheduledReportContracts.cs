namespace Mabuntle.Application.Sellers;

public interface ISellerScheduledReportService
{
    Task<SellerReportScheduleResponse> GetOrCreateScheduleAsync(
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<SellerReportScheduleSaveResult> SaveScheduleAsync(
        Guid sellerId,
        bool isVerifiedSeller,
        SellerReportScheduleRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<SellerReportDigestSendResult> SendTestDigestAsync(
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<SellerScheduledReportProcessingResult> ProcessDueReportsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record SellerReportScheduleRequest(
    bool IsEnabled,
    string Frequency,
    string ReportRange,
    string? SendDayOfWeek,
    int? SendDayOfMonth,
    string SendTimeLocal,
    string TimeZoneId);

public sealed record SellerReportScheduleResponse(
    Guid? ScheduleId,
    bool IsEnabled,
    string Frequency,
    string ReportRange,
    string? SendDayOfWeek,
    int? SendDayOfMonth,
    string SendTimeLocal,
    string TimeZoneId,
    DateTimeOffset? NextRunAtUtc,
    DateTimeOffset? LastSentAtUtc,
    DateTimeOffset? LastReportPeriodStartUtc,
    DateTimeOffset? LastReportPeriodEndUtc,
    string? LastFailureReason,
    DateTimeOffset? LastFailedAtUtc);

public sealed record SellerReportScheduleSaveResult(
    bool IsSuccess,
    SellerReportScheduleResponse? Schedule,
    IReadOnlyDictionary<string, string[]> Errors,
    string? ConflictTitle = null,
    string? ConflictDetail = null)
{
    public static SellerReportScheduleSaveResult Success(SellerReportScheduleResponse schedule) =>
        new(true, schedule, new Dictionary<string, string[]>());

    public static SellerReportScheduleSaveResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, null, errors);

    public static SellerReportScheduleSaveResult Conflict(string title, string detail) =>
        new(false, null, new Dictionary<string, string[]>(), title, detail);
}

public sealed record SellerReportDigestSendResult(
    bool IsSuccess,
    Guid? NotificationId,
    string? FailureReason = null);

public sealed record SellerScheduledReportProcessingResult(
    int ProcessedCount,
    int SentCount,
    int FailedCount,
    int SkippedDuplicateCount);
