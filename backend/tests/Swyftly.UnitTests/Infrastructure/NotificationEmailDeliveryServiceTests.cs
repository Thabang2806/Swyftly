using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Notifications;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Notifications;

namespace Swyftly.UnitTests.Infrastructure;

public sealed class NotificationEmailDeliveryServiceTests
{
    [Fact]
    public async Task ProcessPendingAsync_SendsPendingEmailDelivery()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var delivery = await SeedDeliveryAsync(dbContext, now);
        var provider = new FakeEmailDeliveryProvider(new EmailDeliveryResult(true));
        var service = CreateService(dbContext, provider);

        var result = await service.ProcessPendingAsync(now.AddMinutes(1));

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.SentCount);
        Assert.Single(provider.Messages);
        var updated = await dbContext.NotificationEmailDeliveries.SingleAsync(item => item.Id == delivery.Id);
        Assert.Equal(NotificationEmailDeliveryStatus.Sent, updated.Status);
        Assert.NotNull(updated.SentAtUtc);
    }

    [Fact]
    public async Task ProcessPendingAsync_RetriesFailuresAndMarksTerminalFailureAtMaxAttempts()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var delivery = await SeedDeliveryAsync(dbContext, now);
        var provider = new FakeEmailDeliveryProvider(new EmailDeliveryResult(false, "SMTP unavailable"));
        var service = CreateService(dbContext, provider, maxAttempts: 1);

        var result = await service.ProcessPendingAsync(now.AddMinutes(1));

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.FailedCount);
        var updated = await dbContext.NotificationEmailDeliveries.SingleAsync(item => item.Id == delivery.Id);
        Assert.Equal(NotificationEmailDeliveryStatus.Failed, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.Equal("SMTP unavailable", updated.FailureReason);
    }

    private static async Task<NotificationEmailDelivery> SeedDeliveryAsync(
        SwyftlyDbContext dbContext,
        DateTimeOffset now)
    {
        var notification = new Notification(
            Guid.NewGuid(),
            "OrderShipped",
            "Your order has shipped",
            "The seller marked your order as shipped.",
            "Order",
            Guid.NewGuid(),
            now);
        var delivery = new NotificationEmailDelivery(
            notification.Id,
            "buyer@example.test",
            notification.Title,
            notification.Message,
            now);

        dbContext.Notifications.Add(notification);
        dbContext.NotificationEmailDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync();
        return delivery;
    }

    private static EfNotificationEmailDeliveryService CreateService(
        SwyftlyDbContext dbContext,
        IEmailDeliveryProvider provider,
        int maxAttempts = 3) =>
        new(
            dbContext,
            provider,
            Options.Create(new EmailDeliveryOptions
            {
                FromAddress = "no-reply@swyftly.test",
                FromName = "Swyftly",
                MaxAttempts = maxAttempts,
                RetryMinutes = 15
            }),
            NullLogger<EfNotificationEmailDeliveryService>.Instance);

    private static SwyftlyDbContext CreateDbContext()
    {
        var interceptor = new AuditableEntitySaveChangesInterceptor(new FixedTimeProvider(DateTimeOffset.UtcNow));
        var options = new DbContextOptionsBuilder<SwyftlyDbContext>()
            .UseInMemoryDatabase($"NotificationEmailDeliveryServiceTests-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        return new SwyftlyDbContext(options);
    }

    private sealed class FakeEmailDeliveryProvider(params EmailDeliveryResult[] results) : IEmailDeliveryProvider
    {
        private readonly Queue<EmailDeliveryResult> results = new(results);

        public string ProviderName => "Fake";

        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(
            EmailDeliveryMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(results.Count > 0 ? results.Dequeue() : new EmailDeliveryResult(true));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
