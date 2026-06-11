using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Notifications;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Notifications;

public sealed class EfNotificationEmailDeliveryService(
    MabuntleDbContext dbContext,
    IEmailDeliveryProvider emailDeliveryProvider,
    IOptions<EmailDeliveryOptions> options,
    ILogger<EfNotificationEmailDeliveryService> logger) : INotificationEmailDeliveryService
{
    private readonly EmailDeliveryOptions options = options.Value;

    public async Task<NotificationEmailDeliveryProcessingResult> ProcessPendingAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, options.BatchSize);
        var maxAttempts = Math.Max(1, options.MaxAttempts);
        var retryDelay = TimeSpan.FromMinutes(Math.Max(1, options.RetryMinutes));

        var deliveries = await dbContext.NotificationEmailDeliveries
            .Include(delivery => delivery.Notification)
            .Where(delivery => delivery.Status == NotificationEmailDeliveryStatus.Pending
                && delivery.AttemptCount < maxAttempts
                && delivery.NextAttemptAtUtc <= now)
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .ThenBy(delivery => delivery.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = 0;

        foreach (var delivery in deliveries)
        {
            try
            {
                var result = await emailDeliveryProvider.SendAsync(
                    new EmailDeliveryMessage(
                        delivery.NotificationId,
                        delivery.Id,
                        options.FromAddress,
                        options.FromName,
                        delivery.RecipientEmail,
                        delivery.Subject,
                        delivery.Body),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    delivery.MarkSent(now);
                    sent++;
                }
                else
                {
                    delivery.MarkFailedAttempt(result.FailureReason, now, now.Add(retryDelay), maxAttempts);
                    failed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                delivery.MarkFailedAttempt(exception.Message, now, now.Add(retryDelay), maxAttempts);
                failed++;
                logger.LogWarning(
                    exception,
                    "Email delivery {DeliveryId} failed for notification {NotificationId}.",
                    delivery.Id,
                    delivery.NotificationId);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new NotificationEmailDeliveryProcessingResult(deliveries.Count, sent, failed);
    }
}
