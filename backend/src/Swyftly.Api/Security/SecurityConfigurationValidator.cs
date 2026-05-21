using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Swyftly.Api.Authentication;
using Swyftly.Infrastructure.Notifications;
using Swyftly.Infrastructure.Payments;

namespace Swyftly.Api.Security;

public static class SecurityConfigurationValidator
{
    private const int MinimumSecretLength = 32;

    private static readonly string[] UnsafeSecretMarkers =
    [
        "change_me",
        "development-only",
        "testing-only",
        "placeholder"
    ];

    private static readonly string[] UnsafeConnectionStringMarkers =
    [
        "change_me",
        "development-only",
        "testing-only",
        "placeholder",
        "swyftly_dev_password",
        "Password=postgres",
        "Username=postgres"
    ];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        ValidateProductionConfiguration(configuration);
    }

    public static void ValidateProductionConfiguration(IConfiguration configuration)
    {
        var jwtSigningKey = configuration["Jwt:SigningKey"];
        if (IsWeakOrDefaultSecret(jwtSigningKey))
        {
            throw new InvalidOperationException(
                "Production Jwt:SigningKey must be provided through environment configuration or a secret store and must be at least 32 characters.");
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString)
            || ContainsAny(connectionString, UnsafeConnectionStringMarkers)
            || connectionString.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production ConnectionStrings:DefaultConnection must be provided through environment configuration or a secret store and cannot use local placeholder values.");
        }

        ValidatePaymentProviderConfiguration(configuration);
        ValidateAuthCookieConfiguration(configuration);
        ValidateEmailDeliveryConfiguration(configuration);
    }

    private static void ValidatePaymentProviderConfiguration(IConfiguration configuration)
    {
        var providerName = configuration["PaymentProvider:ProviderName"] ?? "Fake";
        if (string.Equals(providerName, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production PaymentProvider:ProviderName cannot be Fake. Configure a real provider before running in production.");
        }

        if (!string.Equals(providerName, PayFastPaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Production PaymentProvider:ProviderName '{providerName}' is not supported.");
        }

        ValidatePayFastConfiguration(configuration);
    }

    private static void ValidatePayFastConfiguration(IConfiguration configuration)
    {
        RequireNonPlaceholder(configuration, "PayFast:MerchantId");
        RequireNonPlaceholder(configuration, "PayFast:MerchantKey");
        RequireNonPlaceholder(configuration, "PayFast:Passphrase", minimumLength: 8);
        RequireHttpsUrl(configuration, "PayFast:ProcessUrl");
        RequireHttpsUrl(configuration, "PayFast:NotifyUrl");
        RequireHttpsUrl(configuration, "PayFast:CheckoutBridgeBaseUrl");
        RequireHttpsUrl(configuration, "PayFast:ValidateUrl");

        var requireRemoteValidation = configuration["PayFast:RequireRemoteValidation"];
        if (bool.TryParse(requireRemoteValidation, out var enabled) && !enabled)
        {
            throw new InvalidOperationException(
                "Production PayFast:RequireRemoteValidation must not be disabled.");
        }
    }

    private static void ValidateAuthCookieConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(AuthCookieOptions.SectionName);
        var secureValue = section["Secure"];
        var sameSite = SameSiteMode.Lax;
        if (bool.TryParse(secureValue, out var secure) && !secure)
        {
            throw new InvalidOperationException(
                "Production AuthCookies:Secure must be true.");
        }

        var sameSiteValue = section["SameSite"];
        if (!string.IsNullOrWhiteSpace(sameSiteValue)
            && !Enum.TryParse(sameSiteValue, ignoreCase: true, out sameSite))
        {
            throw new InvalidOperationException(
                "Production AuthCookies:SameSite must be Lax or Strict.");
        }

        if (sameSite is SameSiteMode.None or SameSiteMode.Unspecified)
        {
            throw new InvalidOperationException(
                "Production AuthCookies:SameSite must be Lax or Strict.");
        }

        RequireCookiePath(section["RefreshTokenPath"], "AuthCookies:RefreshTokenPath", requireAuthPath: true);
        RequireCookiePath(section["CsrfPath"], "AuthCookies:CsrfPath", requireAuthPath: false);

        var domain = section["Domain"];
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var trimmedDomain = domain.Trim();
            if (trimmedDomain.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                || trimmedDomain.Contains(':', StringComparison.Ordinal)
                || trimmedDomain.Contains('/')
                || ContainsUnsafeMarker(trimmedDomain))
            {
                throw new InvalidOperationException(
                    "Production AuthCookies:Domain must be a production host or parent domain, not localhost or a placeholder.");
            }
        }
    }

    private static void ValidateEmailDeliveryConfiguration(IConfiguration configuration)
    {
        var providerName = configuration["EmailDelivery:ProviderName"] ?? LogOnlyEmailDeliveryProvider.Name;
        if (string.Equals(providerName, LogOnlyEmailDeliveryProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production EmailDelivery:ProviderName cannot be LogOnly. Configure SMTP or another real email delivery provider before running in production.");
        }

        if (!string.Equals(providerName, SmtpEmailDeliveryProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Production EmailDelivery:ProviderName '{providerName}' is not supported.");
        }

        RequireNonPlaceholder(configuration, "EmailDelivery:FromAddress");
        if (!configuration["EmailDelivery:FromAddress"]!.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production EmailDelivery:FromAddress must be a valid email address.");
        }

        RequireNonPlaceholder(configuration, "EmailDelivery:Smtp:Host");
        var smtpPort = configuration["EmailDelivery:Smtp:Port"];
        if (!int.TryParse(smtpPort, out var port) || port <= 0)
        {
            throw new InvalidOperationException(
                "Production EmailDelivery:Smtp:Port must be a valid positive port.");
        }
    }

    private static void RequireCookiePath(string? value, string key, bool requireAuthPath)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production {key} must be an absolute cookie path.");
        }

        if (requireAuthPath && !value.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production AuthCookies:RefreshTokenPath must be scoped to /api/auth.");
        }
    }

    private static void RequireNonPlaceholder(
        IConfiguration configuration,
        string key,
        int minimumLength = 1)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Length < minimumLength
            || ContainsUnsafeMarker(value))
        {
            throw new InvalidOperationException(
                $"Production {key} must be provided through environment configuration or a secret store and cannot use placeholder values.");
        }
    }

    private static void RequireHttpsUrl(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Production {key} must be an external HTTPS URL.");
        }
    }

    private static bool IsWeakOrDefaultSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < MinimumSecretLength)
        {
            return true;
        }

        if (value.Distinct().Count() < 8)
        {
            return true;
        }

        return ContainsUnsafeMarker(value);
    }

    private static bool ContainsUnsafeMarker(string value) =>
        ContainsAny(value, UnsafeSecretMarkers);

    private static bool ContainsAny(string value, IReadOnlyCollection<string> markers) =>
        markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
