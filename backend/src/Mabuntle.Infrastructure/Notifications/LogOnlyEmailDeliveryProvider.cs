using Microsoft.Extensions.Logging;
using Mabuntle.Application.Notifications;

namespace Mabuntle.Infrastructure.Notifications;

public sealed class LogOnlyEmailDeliveryProvider(
    ILogger<LogOnlyEmailDeliveryProvider> logger) : IEmailDeliveryProvider
{
    public const string Name = "LogOnly";

    public string ProviderName => Name;

    public Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Log-only email delivery {DeliveryId} for notification {NotificationId} to {RecipientEmail}: {Subject}",
            message.DeliveryId,
            message.NotificationId,
            message.RecipientEmail,
            message.Subject);

        return Task.FromResult(new EmailDeliveryResult(true));
    }
}
