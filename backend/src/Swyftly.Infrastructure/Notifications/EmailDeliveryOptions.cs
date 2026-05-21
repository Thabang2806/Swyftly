namespace Swyftly.Infrastructure.Notifications;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string ProviderName { get; set; } = LogOnlyEmailDeliveryProvider.Name;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Swyftly";

    public string AppBaseUrl { get; set; } = "http://localhost:4200";

    public int BatchSize { get; set; } = 25;

    public int MaxAttempts { get; set; } = 5;

    public int RetryMinutes { get; set; } = 15;

    public SmtpEmailDeliveryOptions Smtp { get; set; } = new();
}

public sealed class SmtpEmailDeliveryOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;
}
