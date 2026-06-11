namespace Mabuntle.Infrastructure.Payments;

public sealed class PaymentWebhookPayloadRetentionOptions
{
    public const string SectionName = "PaymentWebhookPayloadRetention";

    public bool Enabled { get; set; } = true;

    public int RetentionDays { get; set; } = 90;

    public int BatchSize { get; set; } = 500;
}
