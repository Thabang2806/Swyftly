using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Notifications;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerNotificationEndpoints
{
    public static IEndpointRouteBuilder MapSellerNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var preferenceGroup = app.MapGroup("/api/seller/notification-preferences")
            .WithTags("Seller Notifications")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        preferenceGroup.MapGet("", GetNotificationPreferencesAsync)
            .WithName("GetSellerNotificationPreferences")
            .WithSummary("Returns category-level in-app and email notification preferences for the authenticated seller.")
            .Produces<SellerNotificationPreferencesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        preferenceGroup.MapPut("", UpdateNotificationPreferencesAsync)
            .WithName("UpdateSellerNotificationPreferences")
            .WithSummary("Updates category-level in-app and email notification preferences for the authenticated seller.")
            .Produces<SellerNotificationPreferencesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/seller/notifications")
            .WithTags("Seller Notifications")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("", GetNotificationsAsync)
            .WithName("GetSellerNotifications")
            .WithSummary("Returns in-app notifications for the authenticated seller.")
            .Produces<IReadOnlyCollection<NotificationResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/unread-count", GetUnreadNotificationCountAsync)
            .WithName("GetSellerUnreadNotificationCount")
            .WithSummary("Returns the authenticated seller's unread in-app notification count.")
            .Produces<SellerNotificationsUnreadCountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{notificationId:guid}/read", MarkNotificationReadAsync)
            .WithName("MarkSellerNotificationRead")
            .WithSummary("Marks one seller in-app notification as read.")
            .Produces<NotificationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", MarkAllNotificationsReadAsync)
            .WithName("MarkAllSellerNotificationsRead")
            .WithSummary("Marks all seller in-app notifications as read.")
            .Produces<SellerNotificationsReadAllResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetNotificationPreferencesAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        return seller is null
            ? SellerNotFound()
            : HttpResults.Ok(await MapNotificationPreferencesAsync(seller.SellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateNotificationPreferencesAsync(
        SellerNotificationPreferencesRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (request.Preferences is null)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["preferences"] = ["Preferences are required."]
            });
        }

        var errors = ValidatePreferences(request.Preferences);
        if (errors.Count > 0)
        {
            return HttpResults.ValidationProblem(errors);
        }

        var existing = await dbContext.SellerNotificationPreferences
            .Where(preference => preference.SellerId == seller.SellerId)
            .ToListAsync(cancellationToken);

        foreach (var preferenceRequest in request.Preferences)
        {
            var category = preferenceRequest.Category.Trim();
            var preference = existing.SingleOrDefault(item => item.Category == category);
            if (preference is null)
            {
                dbContext.SellerNotificationPreferences.Add(new SellerNotificationPreference(
                    seller.SellerId,
                    category,
                    preferenceRequest.IsEnabled,
                    preferenceRequest.EmailEnabled ?? true));
            }
            else
            {
                preference.SetChannels(
                    preferenceRequest.IsEnabled,
                    preferenceRequest.EmailEnabled ?? preference.EmailEnabled);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await MapNotificationPreferencesAsync(seller.SellerId, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var notifications = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == seller.UserId && notification.IsInAppVisible)
            .AsNoTracking()
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(notifications.Select(EfNotificationService.Map).ToArray());
    }

    private static async Task<IResult> GetUnreadNotificationCountAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var count = await dbContext.Notifications.CountAsync(
            notification => notification.RecipientUserId == seller.UserId
                && notification.IsInAppVisible
                && notification.ReadAtUtc == null,
            cancellationToken);

        return HttpResults.Ok(new SellerNotificationsUnreadCountResponse(count));
    }

    private static async Task<IResult> MarkNotificationReadAsync(
        Guid notificationId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                existing => existing.Id == notificationId
                    && existing.RecipientUserId == seller.UserId
                    && existing.IsInAppVisible,
                cancellationToken);
        if (notification is null)
        {
            return NotificationNotFound();
        }

        var readAtUtc = timeProvider.GetUtcNow();
        notification.MarkRead(readAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await PublishReadBestEffortAsync(
            realtimePublisher,
            seller.UserId,
            notification.Id,
            notification.ReadAtUtc ?? readAtUtc,
            loggerFactory,
            cancellationToken);

        return HttpResults.Ok(EfNotificationService.Map(notification));
    }

    private static async Task<IResult> MarkAllNotificationsReadAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var unreadNotifications = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == seller.UserId
                && notification.IsInAppVisible
                && notification.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var notification in unreadNotifications)
        {
            notification.MarkRead(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await PublishReadAllBestEffortAsync(
            realtimePublisher,
            seller.UserId,
            now,
            unreadNotifications.Count,
            loggerFactory,
            cancellationToken);

        return HttpResults.Ok(new SellerNotificationsReadAllResponse(unreadNotifications.Count));
    }

    private static Dictionary<string, string[]> ValidatePreferences(
        IReadOnlyCollection<SellerNotificationPreferenceRequest> preferences)
    {
        var errors = new Dictionary<string, string[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var preference in preferences)
        {
            var category = preference.Category.Trim();
            if (!SellerNotificationCategory.IsSupported(category))
            {
                errors["preferences"] = [$"Notification category '{preference.Category}' is not supported."];
                return errors;
            }

            if (!seen.Add(category))
            {
                errors["preferences"] = [$"Notification category '{category}' was provided more than once."];
                return errors;
            }
        }

        return errors;
    }

    private static async Task<SellerNotificationPreferencesResponse> MapNotificationPreferencesAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.SellerNotificationPreferences
            .AsNoTracking()
            .Where(preference => preference.SellerId == sellerId)
            .ToDictionaryAsync(preference => preference.Category, cancellationToken);

        var preferences = SellerNotificationCategory.All
            .Select(category =>
            {
                var hasPreference = stored.TryGetValue(category, out var preference);
                return new SellerNotificationPreferenceResponse(
                    category,
                    !hasPreference || preference!.IsEnabled,
                    !hasPreference || preference!.EmailEnabled);
            })
            .ToArray();

        return new SellerNotificationPreferencesResponse(preferences);
    }

    private static async Task<SellerProfileForNotification?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await dbContext.SellerProfiles
            .Where(seller => seller.UserId == userId)
            .Select(seller => new SellerProfileForNotification(seller.Id, seller.UserId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task PublishReadBestEffortAsync(
        INotificationRealtimePublisher realtimePublisher,
        Guid recipientUserId,
        Guid notificationId,
        DateTimeOffset readAtUtc,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await realtimePublisher.PublishNotificationReadAsync(recipientUserId, notificationId, readAtUtc, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(nameof(SellerNotificationEndpoints)).LogWarning(
                exception,
                "Realtime seller notification read publish failed for notification {NotificationId}.",
                notificationId);
        }
    }

    private static async Task PublishReadAllBestEffortAsync(
        INotificationRealtimePublisher realtimePublisher,
        Guid recipientUserId,
        DateTimeOffset readAtUtc,
        int updatedCount,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await realtimePublisher.PublishNotificationsReadAllAsync(recipientUserId, readAtUtc, updatedCount, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(nameof(SellerNotificationEndpoints)).LogWarning(
                exception,
                "Realtime seller notification read-all publish failed for recipient {RecipientUserId}.",
                recipientUserId);
        }
    }

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerNotifications.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult NotificationNotFound() =>
        HttpResults.Problem(
            title: "SellerNotifications.NotFound",
            detail: "Notification was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private sealed record SellerProfileForNotification(Guid SellerId, Guid UserId);
}

public sealed record SellerNotificationsUnreadCountResponse(int UnreadCount);

public sealed record SellerNotificationsReadAllResponse(int UpdatedCount);

public sealed record SellerNotificationPreferencesRequest(
    IReadOnlyCollection<SellerNotificationPreferenceRequest> Preferences);

public sealed record SellerNotificationPreferenceRequest(
    string Category,
    bool IsEnabled,
    bool? EmailEnabled = null);

public sealed record SellerNotificationPreferencesResponse(
    IReadOnlyCollection<SellerNotificationPreferenceResponse> Preferences);

public sealed record SellerNotificationPreferenceResponse(
    string Category,
    bool IsEnabled,
    bool EmailEnabled);
