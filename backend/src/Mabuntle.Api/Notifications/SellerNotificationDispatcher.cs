using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Notifications;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Api.Notifications;

public static class SellerNotificationDispatcher
{
    public static async Task NotifySellerAsync(
        Guid sellerId,
        string type,
        string title,
        string message,
        string? relatedEntityType,
        Guid? relatedEntityId,
        DateTimeOffset createdAtUtc,
        MabuntleDbContext dbContext,
        INotificationService notificationService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipientUserId = await dbContext.SellerProfiles
                .AsNoTracking()
                .Where(seller => seller.Id == sellerId)
                .Select(seller => (Guid?)seller.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!recipientUserId.HasValue)
            {
                return;
            }

            await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    recipientUserId.Value,
                    type,
                    title,
                    message,
                    relatedEntityType,
                    relatedEntityId,
                    createdAtUtc),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to create seller notification {NotificationType} for seller {SellerId}.",
                type,
                sellerId);
        }
    }
}
