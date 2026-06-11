using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Identity;
using Mabuntle.Infrastructure.Notifications;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_PublishesVisibleInAppNotification()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext);
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "OrderShipped",
            "Order shipped",
            "Your order is on the way.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        var published = Assert.Single(publisher.Created);
        Assert.Equal(result!.NotificationId, published.NotificationId);
    }

    [Fact]
    public async Task CreateAsync_DoesNotPublishHiddenEmailOnlyNotification()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext, configurePreferences: buyer =>
        {
            dbContext.BuyerNotificationPreferences.Add(new BuyerNotificationPreference(
                buyer.Id,
                BuyerNotificationCategory.Reviews,
                isEnabled: false,
                emailEnabled: true));
        });
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "ReviewApproved",
            "Review approved",
            "Your review is visible.",
            "ProductReview",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        Assert.Empty(publisher.Created);
        Assert.Single(await dbContext.NotificationEmailDeliveries.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_SwallowsRealtimePublisherFailure()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedBuyerAsync(dbContext);
        var publisher = new RecordingNotificationRealtimePublisher { ThrowOnCreated = true };
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            "OrderDelivered",
            "Order delivered",
            "Your order was marked delivered.",
            "Order",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        Assert.Single(await dbContext.Notifications.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_QueuesSellerTransactionalEmail()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedSellerAsync(dbContext);
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var result = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            SellerNotificationTypes.ProductApproved,
            "Product approved",
            "Your product was approved.",
            "Product",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.NotNull(result);
        Assert.Single(publisher.Created);
        var delivery = Assert.Single(await dbContext.NotificationEmailDeliveries.ToListAsync());
        Assert.Contains("/seller/products/", delivery.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_RespectsSellerNotificationPreferences()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedSellerAsync(dbContext, seller =>
        {
            dbContext.SellerNotificationPreferences.Add(new SellerNotificationPreference(
                seller.Id,
                SellerNotificationCategory.Products,
                isEnabled: false,
                emailEnabled: true));
            dbContext.SellerNotificationPreferences.Add(new SellerNotificationPreference(
                seller.Id,
                SellerNotificationCategory.Ads,
                isEnabled: false,
                emailEnabled: false));
        });
        var publisher = new RecordingNotificationRealtimePublisher();
        var service = CreateService(dbContext, publisher);

        var emailOnly = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            SellerNotificationTypes.ProductRejected,
            "Product rejected",
            "Your product needs edits.",
            "Product",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
        var suppressed = await service.CreateAsync(new CreateNotificationRequest(
            userId,
            SellerNotificationTypes.AdCampaignRejected,
            "Ad rejected",
            "Your ad needs edits.",
            "AdCampaign",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.NotNull(emailOnly);
        Assert.Null(suppressed);
        Assert.Empty(publisher.Created);

        var notification = Assert.Single(await dbContext.Notifications.ToListAsync());
        Assert.False(notification.IsInAppVisible);
        Assert.Single(await dbContext.NotificationEmailDeliveries.ToListAsync());
    }

    private static EfNotificationService CreateService(
        MabuntleDbContext dbContext,
        INotificationRealtimePublisher publisher) =>
        new(
            dbContext,
            Options.Create(new EmailDeliveryOptions
            {
                FromAddress = "no-reply@mabuntle.test",
                FromName = "Mabuntle",
                AppBaseUrl = "http://localhost:4200"
            }),
            publisher,
            NullLogger<EfNotificationService>.Instance);

    private static async Task<Guid> SeedBuyerAsync(
        MabuntleDbContext dbContext,
        Action<BuyerProfile>? configurePreferences = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"buyer-{Guid.NewGuid():N}@example.test",
            Email = $"buyer-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var buyer = new BuyerProfile(user.Id);
        dbContext.Users.Add(user);
        dbContext.BuyerProfiles.Add(buyer);
        await dbContext.SaveChangesAsync();
        configurePreferences?.Invoke(buyer);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> SeedSellerAsync(
        MabuntleDbContext dbContext,
        Action<SellerProfile>? configurePreferences = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"seller-{Guid.NewGuid():N}@example.test",
            Email = $"seller-{Guid.NewGuid():N}@example.test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var seller = new SellerProfile(user.Id);
        dbContext.Users.Add(user);
        dbContext.SellerProfiles.Add(seller);
        await dbContext.SaveChangesAsync();
        configurePreferences?.Invoke(seller);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static MabuntleDbContext CreateDbContext()
    {
        var interceptor = new AuditableEntitySaveChangesInterceptor(new FixedTimeProvider(DateTimeOffset.UtcNow));
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase($"NotificationServiceTests-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        return new MabuntleDbContext(options);
    }

    private sealed class RecordingNotificationRealtimePublisher : INotificationRealtimePublisher
    {
        public List<NotificationResult> Created { get; } = [];

        public bool ThrowOnCreated { get; init; }

        public Task PublishNotificationCreatedAsync(
            NotificationResult notification,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnCreated)
            {
                throw new InvalidOperationException("Realtime unavailable.");
            }

            Created.Add(notification);
            return Task.CompletedTask;
        }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
