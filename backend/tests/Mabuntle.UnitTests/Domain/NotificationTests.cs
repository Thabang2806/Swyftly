using Mabuntle.Domain.Notifications;

namespace Mabuntle.UnitTests.Domain;

public sealed class NotificationTests
{
    [Fact]
    public void Constructor_CapturesUnreadNotification()
    {
        var recipientUserId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        var notification = new Notification(
            recipientUserId,
            "OrderUpdate",
            "Order shipped",
            "Your order has shipped.",
            "Order",
            Guid.NewGuid(),
            createdAtUtc);

        Assert.Equal(recipientUserId, notification.RecipientUserId);
        Assert.Equal("OrderUpdate", notification.Type);
        Assert.False(notification.IsRead);
        Assert.True(notification.IsInAppVisible);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(createdAtUtc, notification.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_CanCreateHiddenInAppNotificationForEmailOnlyDelivery()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            "ReviewApproved",
            "Review approved",
            "Your review was published.",
            "ProductReview",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            isInAppVisible: false);

        Assert.False(notification.IsInAppVisible);
    }

    [Fact]
    public void MarkRead_SetsReadTimestampOnce()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            "Support",
            "New reply",
            "Support replied to your ticket.",
            null,
            null,
            DateTimeOffset.UtcNow);
        var firstReadAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var secondReadAtUtc = DateTimeOffset.UtcNow.AddMinutes(2);

        notification.MarkRead(firstReadAtUtc);
        notification.MarkRead(secondReadAtUtc);

        Assert.True(notification.IsRead);
        Assert.Equal(firstReadAtUtc, notification.ReadAtUtc);
    }

    [Fact]
    public void Constructor_RejectsInvalidRequiredValues()
    {
        Assert.Throws<ArgumentException>(() => new Notification(Guid.Empty, "Type", "Title", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "", "Title", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "", "Message", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "Title", "", null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Notification(Guid.NewGuid(), "Type", "Title", "Message", null, Guid.Empty, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void EmailDelivery_TracksRetryAndTerminalStates()
    {
        var delivery = new NotificationEmailDelivery(
            Guid.NewGuid(),
            "buyer@example.test",
            "Order shipped",
            "Your order has shipped.",
            DateTimeOffset.UtcNow);
        var failedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        delivery.MarkFailedAttempt("SMTP unavailable", failedAt, failedAt.AddMinutes(15), maxAttempts: 2);

        Assert.Equal(NotificationEmailDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal("SMTP unavailable", delivery.FailureReason);

        delivery.MarkFailedAttempt("Still unavailable", failedAt.AddMinutes(15), null, maxAttempts: 2);

        Assert.Equal(NotificationEmailDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.True(delivery.IsTerminal);
    }

    [Fact]
    public void EmailDelivery_MarkSent_IsTerminal()
    {
        var delivery = new NotificationEmailDelivery(
            Guid.NewGuid(),
            "buyer@example.test",
            "Order shipped",
            "Your order has shipped.",
            DateTimeOffset.UtcNow);
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(1);

        delivery.MarkSent(sentAt);

        Assert.Equal(NotificationEmailDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(sentAt, delivery.SentAtUtc);
        Assert.Null(delivery.NextAttemptAtUtc);
        Assert.True(delivery.IsTerminal);
    }
}
