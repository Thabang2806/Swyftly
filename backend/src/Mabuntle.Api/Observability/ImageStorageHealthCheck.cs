using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Abstractions;
using Mabuntle.Infrastructure.Storage;

namespace Mabuntle.Api.Observability;

public sealed class ImageStorageHealthCheck(
    IImageStorageProvider imageStorageProvider,
    IOptions<MediaScanningOptions> mediaScanningOptions,
    IWebHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var readiness = await imageStorageProvider.CheckReadinessAsync(cancellationToken);
        var failures = readiness.Failures.ToDictionary(
            failure => failure.Key,
            failure => (object)failure.Value,
            StringComparer.OrdinalIgnoreCase);

        var scanningOptions = mediaScanningOptions.Value;
        if (environment.IsProduction() &&
            scanningOptions.RequireExternalScannerInProduction &&
            string.Equals(scanningOptions.ProviderName, TrustLocalCleanMediaMalwareScanner.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            failures["MediaScanning:ProviderName"] = "external-scanner-required-in-production";
        }

        return readiness.IsReady && failures.Count == 0
            ? HealthCheckResult.Healthy(readiness.Description)
            : HealthCheckResult.Unhealthy(
                "Image storage or media scanning configuration is not production ready.",
                data: failures);
    }
}
