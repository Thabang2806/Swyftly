using Microsoft.AspNetCore.SignalR;
using Mabuntle.Application.Notifications;

namespace Mabuntle.Api.Notifications;

public sealed class SignalRNotificationRealtimePublisher(
    IHubContext<NotificationHub> hubContext,
    ILogger<SignalRNotificationRealtimePublisher> logger) : INotificationRealtimePublisher
{
    public Task PublishNotificationCreatedAsync(
        NotificationResult notification,
        CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            notification.RecipientUserId,
            "notificationCreated",
            notification,
            cancellationToken);

    public Task PublishNotificationReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            recipientUserId,
            "notificationRead",
            new NotificationReadRealtimeEvent(notificationId, readAtUtc),
            cancellationToken);

    public Task PublishNotificationsReadAllAsync(
        Guid recipientUserId,
        DateTimeOffset readAtUtc,
        int updatedCount,
        CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            recipientUserId,
            "notificationsReadAll",
            new NotificationsReadAllRealtimeEvent(readAtUtc, updatedCount),
            cancellationToken);

    private async Task SendBestEffortAsync(
        Guid recipientUserId,
        string methodName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .User(recipientUserId.ToString())
                .SendAsync(methodName, payload, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Realtime notification event {MethodName} failed for user {RecipientUserId}.",
                methodName,
                recipientUserId);
        }
    }
}
