using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Mabuntle.Infrastructure.Notifications;

namespace Mabuntle.Api.Notifications;

public sealed class EmailDeliveryHealthCheck(
    IOptions<EmailDeliveryOptions> options,
    IWebHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var current = options.Value;
        var failures = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var provider = current.ProviderName?.Trim();

        if (string.Equals(provider, LogOnlyEmailDeliveryProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (environment.IsProduction())
            {
                failures["EmailDelivery:ProviderName"] = "log-only-not-allowed-in-production";
            }

            return Task.FromResult(failures.Count == 0
                ? HealthCheckResult.Healthy("Log-only email delivery provider is selected.")
                : HealthCheckResult.Unhealthy("Email delivery configuration is not production ready.", data: failures));
        }

        if (!string.Equals(provider, SmtpEmailDeliveryProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            failures["EmailDelivery:ProviderName"] = "unsupported-provider";
            return Task.FromResult(HealthCheckResult.Unhealthy("Email delivery provider is not supported.", data: failures));
        }

        RequireEmail(current.FromAddress, "EmailDelivery:FromAddress", failures);
        RequireValue(current.Smtp.Host, "EmailDelivery:Smtp:Host", failures);
        if (current.Smtp.Port <= 0)
        {
            failures["EmailDelivery:Smtp:Port"] = "invalid-port";
        }

        return Task.FromResult(failures.Count == 0
            ? HealthCheckResult.Healthy("SMTP email delivery configuration is present.")
            : HealthCheckResult.Unhealthy("SMTP email delivery configuration is incomplete.", data: failures));
    }

    private static void RequireValue(string? value, string key, IDictionary<string, object> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures[key] = "missing";
        }
    }

    private static void RequireEmail(string? value, string key, IDictionary<string, object> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            failures[key] = "missing-or-invalid";
        }
    }
}
