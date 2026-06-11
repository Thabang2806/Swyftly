using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Payments;
using Mabuntle.Infrastructure.Payments;

namespace Mabuntle.Api.Payments;

public sealed class PaymentProviderHealthCheck(
    IOptions<PaymentProviderOptions> paymentProviderOptions,
    IOptions<PayFastOptions> payFastOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var providerName = paymentProviderOptions.Value.ProviderName;
        if (string.Equals(providerName, FakePaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Fake payment provider is selected."));
        }

        if (string.Equals(providerName, DisabledPaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Payment provider is intentionally disabled; buyer payment initiation will return a clear unavailable response."));
        }

        if (string.Equals(providerName, PayFastPaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckPayFastOptions(payFastOptions.Value));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Configured payment provider '{providerName}' is not supported."));
    }

    private static HealthCheckResult CheckPayFastOptions(PayFastOptions options)
    {
        var failures = new List<string>();

        RequireValue(options.MerchantId, "PayFast:MerchantId", failures);
        RequireValue(options.MerchantKey, "PayFast:MerchantKey", failures);
        RequireAbsoluteUrl(options.ProcessUrl, "PayFast:ProcessUrl", failures);
        RequireAbsoluteUrl(options.NotifyUrl, "PayFast:NotifyUrl", failures);
        RequireAbsoluteUrl(options.CheckoutBridgeBaseUrl, "PayFast:CheckoutBridgeBaseUrl", failures);

        if (options.RequireRemoteValidation)
        {
            RequireAbsoluteUrl(options.ValidateUrl, "PayFast:ValidateUrl", failures);
        }

        if (failures.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                "PayFast payment provider configuration is incomplete.",
                data: failures.ToDictionary(failure => failure, _ => (object)"missing-or-invalid"));
        }

        var description = options.RequireRemoteValidation
            ? "PayFast payment provider configuration is ready with remote ITN validation enabled."
            : "PayFast payment provider configuration is ready with remote ITN validation disabled for development/testing.";

        return HealthCheckResult.Healthy(description);
    }

    private static void RequireValue(string value, string key, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(key);
        }
    }

    private static void RequireAbsoluteUrl(string value, string key, ICollection<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            failures.Add(key);
        }
    }
}
