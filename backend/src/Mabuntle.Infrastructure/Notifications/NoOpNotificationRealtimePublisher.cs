using Mabuntle.Application.Notifications;

namespace Mabuntle.Infrastructure.Notifications;

public sealed class NoOpNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishNotificationCreatedAsync(
        NotificationResult notification,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishNotificationReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishNotificationsReadAllAsync(
        Guid recipientUserId,
        DateTimeOffset readAtUtc,
        int updatedCount,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
