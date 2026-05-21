using Swyftly.Application.Notifications;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Notifications;
using Swyftly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Swyftly.Infrastructure.Notifications;

public sealed class EfNotificationService(
    SwyftlyDbContext dbContext,
    IOptions<EmailDeliveryOptions> emailOptions) : INotificationService
{
    private readonly EmailDeliveryOptions emailOptions = emailOptions.Value;

    private static readonly IReadOnlyDictionary<string, string> TypeCategories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderTrackingAdded"] = BuyerNotificationCategory.Orders,
            ["OrderReadyToShip"] = BuyerNotificationCategory.Orders,
            ["OrderShipped"] = BuyerNotificationCategory.Orders,
            ["OrderDelivered"] = BuyerNotificationCategory.Orders,
            ["OrderDeliveryFailed"] = BuyerNotificationCategory.Orders,
            ["OrderReturnedToSender"] = BuyerNotificationCategory.Orders,
            ["ReturnApproved"] = BuyerNotificationCategory.Returns,
            ["ReturnRejected"] = BuyerNotificationCategory.Returns,
            ["ReviewApproved"] = BuyerNotificationCategory.Reviews,
            ["ReviewRejected"] = BuyerNotificationCategory.Reviews,
            ["SupportReply"] = BuyerNotificationCategory.Support
        };

    public async Task<NotificationResult?> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var channels = await ResolveChannelsAsync(request, cancellationToken);
        if (!channels.IsInAppEnabled && !channels.IsEmailEnabled)
        {
            return null;
        }

        var notification = new Notification(
            request.RecipientUserId,
            request.Type,
            request.Title,
            request.Message,
            request.RelatedEntityType,
            request.RelatedEntityId,
            request.CreatedAtUtc,
            channels.IsInAppEnabled);

        dbContext.Notifications.Add(notification);

        if (channels.IsEmailEnabled && !string.IsNullOrWhiteSpace(channels.RecipientEmail))
        {
            dbContext.NotificationEmailDeliveries.Add(new NotificationEmailDelivery(
                notification.Id,
                channels.RecipientEmail,
                notification.Title,
                CreateEmailBody(notification),
                request.CreatedAtUtc));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(notification);
    }

    private async Task<NotificationChannelDecision> ResolveChannelsAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TypeCategories.TryGetValue(request.Type, out var category))
        {
            return new NotificationChannelDecision(
                IsInAppEnabled: true,
                IsEmailEnabled: false,
                RecipientEmail: null);
        }

        var buyer = await dbContext.BuyerProfiles
            .Where(profile => profile.UserId == request.RecipientUserId)
            .Select(profile => new
            {
                profile.Id,
                UserEmail = dbContext.Users
                    .Where(user => user.Id == profile.UserId)
                    .Select(user => user.Email)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (buyer is null)
        {
            return new NotificationChannelDecision(
                IsInAppEnabled: true,
                IsEmailEnabled: false,
                RecipientEmail: null);
        }

        var preference = await dbContext.BuyerNotificationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existing => existing.BuyerId == buyer.Id && existing.Category == category,
                cancellationToken);

        return new NotificationChannelDecision(
            IsInAppEnabled: preference?.IsEnabled ?? true,
            IsEmailEnabled: preference?.EmailEnabled ?? true,
            RecipientEmail: buyer.UserEmail);
    }

    public static NotificationResult Map(Notification notification) =>
        new(
            notification.Id,
            notification.RecipientUserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.ReadAtUtc,
            notification.CreatedAtUtc);

    private string CreateEmailBody(Notification notification)
    {
        var link = CreateRelatedLink(notification);
        var lines = new List<string>
        {
            notification.Message
        };

        if (!string.IsNullOrWhiteSpace(link))
        {
            lines.Add(string.Empty);
            lines.Add($"Open in Swyftly: {link}");
        }

        lines.Add(string.Empty);
        lines.Add("This is a transactional Swyftly account notification.");
        return string.Join(Environment.NewLine, lines);
    }

    private string? CreateRelatedLink(Notification notification)
    {
        var path = notification.RelatedEntityType switch
        {
            "Order" when notification.RelatedEntityId.HasValue => $"/account/orders/{notification.RelatedEntityId.Value}",
            "ReturnRequest" when notification.RelatedEntityId.HasValue => $"/account/returns/{notification.RelatedEntityId.Value}",
            "SupportTicket" when notification.RelatedEntityId.HasValue => $"/account/support/{notification.RelatedEntityId.Value}",
            "ProductReview" => "/account/reviews",
            _ => "/account/notifications"
        };

        var baseUrl = emailOptions.AppBaseUrl.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? path
            : $"{baseUrl}{path}";
    }

    private sealed record NotificationChannelDecision(
        bool IsInAppEnabled,
        bool IsEmailEnabled,
        string? RecipientEmail);
}
