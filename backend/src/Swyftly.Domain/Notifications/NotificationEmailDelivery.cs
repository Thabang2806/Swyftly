using Swyftly.Domain.Common;

namespace Swyftly.Domain.Notifications;

public sealed class NotificationEmailDelivery : AuditableEntity
{
    private NotificationEmailDelivery()
    {
    }

    public NotificationEmailDelivery(
        Guid notificationId,
        string recipientEmail,
        string subject,
        string body,
        DateTimeOffset nextAttemptAtUtc)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id is required.", nameof(notificationId));
        }

        NotificationId = notificationId;
        RecipientEmail = Required(recipientEmail, nameof(recipientEmail), maxLength: 320);
        Subject = Required(subject, nameof(subject), maxLength: 200);
        Body = Required(body, nameof(body), maxLength: 4000);
        Status = NotificationEmailDeliveryStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public Guid NotificationId { get; private set; }

    public Notification Notification { get; private set; } = null!;

    public string RecipientEmail { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public NotificationEmailDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public bool IsTerminal => Status is NotificationEmailDeliveryStatus.Sent or NotificationEmailDeliveryStatus.Failed;

    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        if (IsTerminal)
        {
            return;
        }

        AttemptCount++;
        Status = NotificationEmailDeliveryStatus.Sent;
        SentAtUtc = sentAtUtc;
        NextAttemptAtUtc = null;
        FailureReason = null;
    }

    public void MarkFailedAttempt(
        string? failureReason,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc,
        int maxAttempts)
    {
        if (IsTerminal)
        {
            return;
        }

        AttemptCount++;
        FailureReason = Optional(failureReason, maxLength: 1000) ?? "Email delivery failed.";

        if (AttemptCount >= maxAttempts || retryAtUtc is null)
        {
            Status = NotificationEmailDeliveryStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }

        Status = NotificationEmailDeliveryStatus.Pending;
        NextAttemptAtUtc = retryAtUtc.Value > failedAtUtc ? retryAtUtc.Value : failedAtUtc;
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
