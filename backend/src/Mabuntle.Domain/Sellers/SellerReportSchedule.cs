using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Sellers;

public sealed class SellerReportSchedule : AuditableEntity
{
    public const int TimeZoneIdMaxLength = 100;
    public const int SendTimeLocalMaxLength = 5;
    public const int ErrorMaxLength = 500;

    private SellerReportSchedule()
    {
        SendTimeLocal = string.Empty;
        TimeZoneId = string.Empty;
    }

    public SellerReportSchedule(Guid sellerId, DateTimeOffset createdAtUtc)
    {
        SellerId = sellerId;
        IsEnabled = false;
        Frequency = SellerReportFrequency.Weekly;
        ReportRange = SellerReportRange.Last30Days;
        SendDayOfWeek = DayOfWeek.Monday;
        SendDayOfMonth = null;
        SendTimeLocal = "08:00";
        TimeZoneId = "Africa/Johannesburg";
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid SellerId { get; private set; }

    public bool IsEnabled { get; private set; }

    public SellerReportFrequency Frequency { get; private set; }

    public SellerReportRange ReportRange { get; private set; }

    public DayOfWeek? SendDayOfWeek { get; private set; }

    public int? SendDayOfMonth { get; private set; }

    public string SendTimeLocal { get; private set; } = string.Empty;

    public string TimeZoneId { get; private set; } = string.Empty;

    public DateTimeOffset? NextRunAtUtc { get; private set; }

    public DateTimeOffset? LastSentAtUtc { get; private set; }

    public DateTimeOffset? LastReportPeriodStartUtc { get; private set; }

    public DateTimeOffset? LastReportPeriodEndUtc { get; private set; }

    public string? LastFailureReason { get; private set; }

    public DateTimeOffset? LastFailedAtUtc { get; private set; }

    public void Update(
        bool isEnabled,
        SellerReportFrequency frequency,
        SellerReportRange reportRange,
        DayOfWeek? sendDayOfWeek,
        int? sendDayOfMonth,
        string sendTimeLocal,
        string timeZoneId,
        DateTimeOffset? nextRunAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        IsEnabled = isEnabled;
        Frequency = frequency;
        ReportRange = reportRange;
        SendDayOfWeek = sendDayOfWeek;
        SendDayOfMonth = sendDayOfMonth;
        SendTimeLocal = sendTimeLocal;
        TimeZoneId = timeZoneId;
        NextRunAtUtc = isEnabled ? nextRunAtUtc : null;
        LastFailureReason = null;
        LastFailedAtUtc = null;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkSent(
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        DateTimeOffset sentAtUtc,
        DateTimeOffset? nextRunAtUtc)
    {
        LastSentAtUtc = sentAtUtc;
        LastReportPeriodStartUtc = periodStartUtc;
        LastReportPeriodEndUtc = periodEndUtc;
        LastFailureReason = null;
        LastFailedAtUtc = null;
        NextRunAtUtc = IsEnabled ? nextRunAtUtc : null;
        UpdatedAtUtc = sentAtUtc;
    }

    public void MarkFailed(string reason, DateTimeOffset failedAtUtc, DateTimeOffset? nextRunAtUtc)
    {
        LastFailureReason = Trim(reason, ErrorMaxLength);
        LastFailedAtUtc = failedAtUtc;
        NextRunAtUtc = IsEnabled ? nextRunAtUtc : null;
        UpdatedAtUtc = failedAtUtc;
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
