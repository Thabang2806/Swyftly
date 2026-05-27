namespace Swyftly.Worker;

using Swyftly.Application.Inventory;
using Swyftly.Application.Media;
using Swyftly.Application.Notifications;
using Swyftly.Application.Orders;
using Swyftly.Application.Payments;
using Swyftly.Application.Sellers;

public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Swyftly worker expiring inventory reservations at {Time}", timeProvider.GetUtcNow());
            }

            await ExpireReservationsAsync(stoppingToken);
            await RedactExpiredPaymentWebhookPayloadsAsync(stoppingToken);
            await CleanupMediaAsync(stoppingToken);
            await ProcessNotificationEmailsAsync(stoppingToken);
            await ProcessSellerScheduledReportsAsync(stoppingToken);
            await SyncCarrierTrackingAsync(stoppingToken);
            await Task.Delay(IdleDelay, stoppingToken);
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var reservationService = scope.ServiceProvider.GetRequiredService<IInventoryReservationService>();
            var result = await reservationService.ExpireReservationsAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.IsSuccess && result.Value.Count > 0)
            {
                logger.LogInformation("Expired {Count} inventory reservations.", result.Value.Count);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Inventory reservation expiry placeholder failed.");
        }
    }

    private async Task RedactExpiredPaymentWebhookPayloadsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<IPaymentWebhookPayloadRetentionService>();
            var result = await retentionService.RedactExpiredPayloadsAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.RedactedCount > 0)
            {
                logger.LogInformation(
                    "Redacted {Count} expired payment webhook payloads older than {CutoffUtc}.",
                    result.RedactedCount,
                    result.CutoffUtc);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Payment webhook payload retention cleanup failed.");
        }
    }

    private async Task CleanupMediaAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<IMediaCleanupService>();
            var result = await cleanupService.CleanupAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Processed {ProcessedCount} media cleanup candidates; deleted {DeletedCount}, failed {FailedCount}.",
                    result.ProcessedCount,
                    result.DeletedCount,
                    result.FailedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media cleanup failed.");
        }
    }

    private async Task ProcessNotificationEmailsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var emailDeliveryService = scope.ServiceProvider.GetRequiredService<INotificationEmailDeliveryService>();
            var result = await emailDeliveryService.ProcessPendingAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Processed {ProcessedCount} notification email deliveries; sent {SentCount}, failed {FailedCount}.",
                    result.ProcessedCount,
                    result.SentCount,
                    result.FailedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Notification email delivery processing failed.");
        }
    }

    private async Task SyncCarrierTrackingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var carrierTrackingSyncService = scope.ServiceProvider.GetRequiredService<ICarrierTrackingSyncService>();
            var result = await carrierTrackingSyncService.SyncDueShipmentsAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Processed {ProcessedCount} carrier tracking sync candidates; updated {UpdatedCount}, failed {FailedCount}.",
                    result.ProcessedCount,
                    result.UpdatedCount,
                    result.FailedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Carrier tracking sync failed.");
        }
    }

    private async Task ProcessSellerScheduledReportsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<ISellerScheduledReportService>();
            var result = await reportService.ProcessDueReportsAsync(timeProvider.GetUtcNow(), stoppingToken);

            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Processed {ProcessedCount} seller scheduled reports; sent {SentCount}, failed {FailedCount}, skipped duplicates {SkippedDuplicateCount}.",
                    result.ProcessedCount,
                    result.SentCount,
                    result.FailedCount,
                    result.SkippedDuplicateCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Seller scheduled analytics report processing failed.");
        }
    }
}
