namespace Mabuntle.Application.Notifications;

public interface INotificationService
{
    Task<NotificationResult?> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public interface INotificationRealtimePublisher
{
    Task PublishNotificationCreatedAsync(
        NotificationResult notification,
        CancellationToken cancellationToken = default);

    Task PublishNotificationReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default);

    Task PublishNotificationsReadAllAsync(
        Guid recipientUserId,
        DateTimeOffset readAtUtc,
        int updatedCount,
        CancellationToken cancellationToken = default);
}

public interface IEmailDeliveryProvider
{
    string ProviderName { get; }

    Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryMessage message,
        CancellationToken cancellationToken = default);
}

public interface INotificationEmailDeliveryService
{
    Task<NotificationEmailDeliveryProcessingResult> ProcessPendingAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationResult(
    Guid NotificationId,
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationReadRealtimeEvent(
    Guid NotificationId,
    DateTimeOffset ReadAtUtc);

public sealed record NotificationsReadAllRealtimeEvent(
    DateTimeOffset ReadAtUtc,
    int UpdatedCount);

public sealed record EmailDeliveryMessage(
    Guid NotificationId,
    Guid DeliveryId,
    string FromAddress,
    string FromName,
    string RecipientEmail,
    string Subject,
    string Body);

public sealed record EmailDeliveryResult(
    bool IsSuccess,
    string? FailureReason = null);

public sealed record NotificationEmailDeliveryProcessingResult(
    int ProcessedCount,
    int SentCount,
    int FailedCount);
