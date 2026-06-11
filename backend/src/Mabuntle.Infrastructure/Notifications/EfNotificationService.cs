using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Notifications;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mabuntle.Infrastructure.Notifications;

public sealed class EfNotificationService(
    MabuntleDbContext dbContext,
    IOptions<EmailDeliveryOptions> emailOptions,
    INotificationRealtimePublisher realtimePublisher,
    ILogger<EfNotificationService> logger) : INotificationService
{
    private readonly EmailDeliveryOptions emailOptions = emailOptions.Value;

    private static readonly IReadOnlyDictionary<string, string> BuyerTypeCategories =
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

    private static readonly IReadOnlyDictionary<string, string> SellerTypeCategories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SellerNotificationTypes.SellerVerificationApproved] = SellerNotificationCategory.Verification,
            [SellerNotificationTypes.SellerVerificationRejected] = SellerNotificationCategory.Verification,
            [SellerNotificationTypes.SellerSuspended] = SellerNotificationCategory.Verification,
            [SellerNotificationTypes.ProductApproved] = SellerNotificationCategory.Products,
            [SellerNotificationTypes.ProductRejected] = SellerNotificationCategory.Products,
            [SellerNotificationTypes.ProductChangesRequested] = SellerNotificationCategory.Products,
            [SellerNotificationTypes.ProductListingRevisionApproved] = SellerNotificationCategory.Revisions,
            [SellerNotificationTypes.ProductListingRevisionRejected] = SellerNotificationCategory.Revisions,
            [SellerNotificationTypes.ProductVariantRevisionApproved] = SellerNotificationCategory.Revisions,
            [SellerNotificationTypes.ProductVariantRevisionRejected] = SellerNotificationCategory.Revisions,
            [SellerNotificationTypes.AdCampaignApproved] = SellerNotificationCategory.Ads,
            [SellerNotificationTypes.AdCampaignRejected] = SellerNotificationCategory.Ads,
            [SellerNotificationTypes.SellerAnalyticsDigestReady] = SellerNotificationCategory.Reports
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

        if (notification.IsInAppVisible)
        {
            await PublishCreatedBestEffortAsync(notification, cancellationToken);
        }

        return Map(notification);
    }

    private async Task PublishCreatedBestEffortAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await realtimePublisher.PublishNotificationCreatedAsync(Map(notification), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Realtime notification publish failed for notification {NotificationId}.",
                notification.Id);
        }
    }

    private async Task<NotificationChannelDecision> ResolveChannelsAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (BuyerTypeCategories.TryGetValue(request.Type, out var category))
        {
            return await ResolveBuyerChannelsAsync(request, category, cancellationToken);
        }

        if (SellerTypeCategories.TryGetValue(request.Type, out var sellerCategory))
        {
            return await ResolveSellerChannelsAsync(request, sellerCategory, cancellationToken);
        }

        return new NotificationChannelDecision(
            IsInAppEnabled: true,
            IsEmailEnabled: false,
            RecipientEmail: null);
    }

    private async Task<NotificationChannelDecision> ResolveBuyerChannelsAsync(
        CreateNotificationRequest request,
        string category,
        CancellationToken cancellationToken)
    {
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

    private async Task<NotificationChannelDecision> ResolveSellerChannelsAsync(
        CreateNotificationRequest request,
        string category,
        CancellationToken cancellationToken)
    {
        var seller = await dbContext.SellerProfiles
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

        if (seller is null)
        {
            return new NotificationChannelDecision(
                IsInAppEnabled: true,
                IsEmailEnabled: false,
                RecipientEmail: null);
        }

        var preference = await dbContext.SellerNotificationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existing => existing.SellerId == seller.Id && existing.Category == category,
                cancellationToken);

        return new NotificationChannelDecision(
            IsInAppEnabled: preference?.IsEnabled ?? true,
            IsEmailEnabled: preference?.EmailEnabled ?? true,
            RecipientEmail: seller.UserEmail);
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
            lines.Add($"Open in Mabuntle: {link}");
        }

        lines.Add(string.Empty);
        lines.Add("This is a transactional Mabuntle account notification.");
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
            "SellerProfile" => "/seller",
            "SellerAnalytics" => "/seller/analytics",
            "Product" when notification.RelatedEntityId.HasValue => $"/seller/products/{notification.RelatedEntityId.Value}/edit",
            "AdCampaign" when notification.RelatedEntityId.HasValue => $"/seller/ads/{notification.RelatedEntityId.Value}",
            _ when SellerTypeCategories.ContainsKey(notification.Type) => "/seller/notifications",
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
