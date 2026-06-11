using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Payments;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Payments;

public sealed class EfPaymentWebhookPayloadRetentionService(
    MabuntleDbContext dbContext,
    IOptions<PaymentWebhookPayloadRetentionOptions> options)
    : IPaymentWebhookPayloadRetentionService
{
    private readonly PaymentWebhookPayloadRetentionOptions _options = options.Value;

    public async Task<PaymentWebhookPayloadRetentionResult> RedactExpiredPayloadsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var retentionDays = Math.Max(1, _options.RetentionDays);
        var cutoffUtc = now.AddDays(-retentionDays);
        if (!_options.Enabled)
        {
            return new PaymentWebhookPayloadRetentionResult(0, cutoffUtc);
        }

        var batchSize = Math.Clamp(_options.BatchSize, 1, 5000);
        var expiredEvents = await dbContext.PaymentEvents
            .Where(paymentEvent => paymentEvent.RawPayloadRedactedAtUtc == null
                && paymentEvent.ReceivedAtUtc < cutoffUtc)
            .OrderBy(paymentEvent => paymentEvent.ReceivedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var paymentEvent in expiredEvents)
        {
            paymentEvent.RedactRawPayload(CreateRedactedPayload(paymentEvent.Provider, now), now);
        }

        if (expiredEvents.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PaymentWebhookPayloadRetentionResult(expiredEvents.Count, cutoffUtc);
    }

    private static string CreateRedactedPayload(string provider, DateTimeOffset redactedAtUtc) =>
        JsonSerializer.Serialize(new
        {
            provider,
            payloadType = "redacted",
            redacted = true,
            redactedAtUtc,
            reason = "retention_expired"
        });
}
