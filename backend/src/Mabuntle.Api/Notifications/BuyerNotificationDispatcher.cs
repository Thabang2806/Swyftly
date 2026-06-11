using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Notifications;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Api.Notifications;

public static class BuyerNotificationDispatcher
{
    public static async Task NotifyBuyerAsync(
        Guid buyerId,
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
            var recipientUserId = await dbContext.BuyerProfiles
                .AsNoTracking()
                .Where(buyer => buyer.Id == buyerId)
                .Select(buyer => (Guid?)buyer.UserId)
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
                "Failed to create buyer notification {NotificationType} for buyer {BuyerId}.",
                type,
                buyerId);
        }
    }
}
