using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Swyftly.Application.Notifications;

namespace Swyftly.Infrastructure.Notifications;

public sealed class SmtpEmailDeliveryProvider(
    IOptions<EmailDeliveryOptions> options) : IEmailDeliveryProvider
{
    public const string Name = "Smtp";

    private readonly EmailDeliveryOptions options = options.Value;

    public string ProviderName => Name;

    public async Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(message.FromAddress, message.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false
            };
            mail.To.Add(new MailAddress(message.RecipientEmail));

            using var client = new SmtpClient(options.Smtp.Host, options.Smtp.Port)
            {
                EnableSsl = options.Smtp.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(options.Smtp.Username))
            {
                client.Credentials = new NetworkCredential(
                    options.Smtp.Username,
                    options.Smtp.Password);
            }

            await client.SendMailAsync(mail, cancellationToken);
            return new EmailDeliveryResult(true);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            return new EmailDeliveryResult(false, exception.Message);
        }
    }
}
