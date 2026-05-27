using Swyftly.Domain.Common;

namespace Swyftly.Domain.Sellers;

public sealed class SellerReportScheduleRun : Entity
{
    public const int FailureReasonMaxLength = 500;

    private SellerReportScheduleRun()
    {
    }

    public SellerReportScheduleRun(
        Guid sellerReportScheduleId,
        Guid sellerId,
        DateTimeOffset reportPeriodStartUtc,
        DateTimeOffset reportPeriodEndUtc,
        DateTimeOffset createdAtUtc)
    {
        SellerReportScheduleId = sellerReportScheduleId;
        SellerId = sellerId;
        ReportPeriodStartUtc = reportPeriodStartUtc;
        ReportPeriodEndUtc = reportPeriodEndUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SellerReportScheduleId { get; private set; }

    public Guid SellerId { get; private set; }

    public DateTimeOffset ReportPeriodStartUtc { get; private set; }

    public DateTimeOffset ReportPeriodEndUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public Guid? NotificationId { get; private set; }

    public bool IsSuccess { get; private set; }

    public string? FailureReason { get; private set; }

    public void MarkSent(Guid? notificationId, DateTimeOffset completedAtUtc)
    {
        NotificationId = notificationId;
        CompletedAtUtc = completedAtUtc;
        IsSuccess = true;
        FailureReason = null;
    }

    public void MarkFailed(string reason, DateTimeOffset completedAtUtc)
    {
        CompletedAtUtc = completedAtUtc;
        IsSuccess = false;
        FailureReason = Trim(reason);
    }

    private static string Trim(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= FailureReasonMaxLength ? trimmed : trimmed[..FailureReasonMaxLength];
    }
}
