using Microsoft.Extensions.Configuration;
using Swyftly.Api.Security;

namespace Swyftly.IntegrationTests;

public sealed class SecurityConfigurationValidatorTests
{
    [Fact]
    public void ValidateProductionConfiguration_RejectsDefaultJwtSigningKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db.internal;Port=5432;Database=swyftly;Username=swyftly_app;Password=ProdDbValueForTests2026!",
            ["Jwt:SigningKey"] = "development-only-signing-key-change-before-production",
            ["PaymentProvider:ProviderName"] = "PayFast",
            ["PayFast:MerchantId"] = "100001",
            ["PayFast:MerchantKey"] = "prod-merchant-key",
            ["PayFast:Passphrase"] = "prod-payfast-passphrase",
            ["PayFast:ProcessUrl"] = "https://www.payfast.co.za/eng/process",
            ["PayFast:NotifyUrl"] = "https://api.swyftly.example/api/payments/webhook/payfast",
            ["PayFast:CheckoutBridgeBaseUrl"] = "https://api.swyftly.example",
            ["PayFast:ValidateUrl"] = "https://www.payfast.co.za/eng/query/validate",
            ["PayFast:RequireRemoteValidation"] = "true"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_AllowsStrongExternalValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db.internal;Port=5432;Database=swyftly;Username=swyftly_app;Password=ProdDbValueForTests2026!",
            ["Jwt:SigningKey"] = "ProdJwtSigningKeyValueForSwyftly2026!!",
            ["PaymentProvider:ProviderName"] = "PayFast",
            ["PayFast:MerchantId"] = "100001",
            ["PayFast:MerchantKey"] = "prod-merchant-key",
            ["PayFast:Passphrase"] = "prod-payfast-passphrase",
            ["PayFast:ProcessUrl"] = "https://www.payfast.co.za/eng/process",
            ["PayFast:NotifyUrl"] = "https://api.swyftly.example/api/payments/webhook/payfast",
            ["PayFast:CheckoutBridgeBaseUrl"] = "https://api.swyftly.example",
            ["PayFast:ValidateUrl"] = "https://www.payfast.co.za/eng/query/validate",
            ["PayFast:RequireRemoteValidation"] = "true",
            ["AuthCookies:Secure"] = "true",
            ["AuthCookies:SameSite"] = "Lax",
            ["AuthCookies:RefreshTokenPath"] = "/api/auth",
            ["AuthCookies:CsrfPath"] = "/",
            ["AuthCookies:Domain"] = ".swyftly.example",
            ["EmailDelivery:ProviderName"] = "Smtp",
            ["EmailDelivery:FromAddress"] = "no-reply@swyftly.example",
            ["EmailDelivery:Smtp:Host"] = "smtp.swyftly.example",
            ["EmailDelivery:Smtp:Port"] = "587"
        });

        SecurityConfigurationValidator.ValidateProductionConfiguration(configuration);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsFakePaymentProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db.internal;Port=5432;Database=swyftly;Username=swyftly_app;Password=ProdDbValueForTests2026!",
            ["Jwt:SigningKey"] = "ProdJwtSigningKeyValueForSwyftly2026!!",
            ["PaymentProvider:ProviderName"] = "Fake"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("cannot be Fake", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsDisabledPayFastRemoteValidation()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db.internal;Port=5432;Database=swyftly;Username=swyftly_app;Password=ProdDbValueForTests2026!",
            ["Jwt:SigningKey"] = "ProdJwtSigningKeyValueForSwyftly2026!!",
            ["PaymentProvider:ProviderName"] = "PayFast",
            ["PayFast:MerchantId"] = "100001",
            ["PayFast:MerchantKey"] = "prod-merchant-key",
            ["PayFast:Passphrase"] = "prod-payfast-passphrase",
            ["PayFast:ProcessUrl"] = "https://www.payfast.co.za/eng/process",
            ["PayFast:NotifyUrl"] = "https://api.swyftly.example/api/payments/webhook/payfast",
            ["PayFast:CheckoutBridgeBaseUrl"] = "https://api.swyftly.example",
            ["PayFast:ValidateUrl"] = "https://www.payfast.co.za/eng/query/validate",
            ["PayFast:RequireRemoteValidation"] = "false"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("RequireRemoteValidation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsInsecureAuthCookies()
    {
        var configuration = BuildValidProductionConfiguration();
        configuration["AuthCookies:Secure"] = "false";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("AuthCookies:Secure", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsCrossSiteAuthCookies()
    {
        var configuration = BuildValidProductionConfiguration();
        configuration["AuthCookies:SameSite"] = "None";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("AuthCookies:SameSite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsRefreshCookieOutsideAuthPath()
    {
        var configuration = BuildValidProductionConfiguration();
        configuration["AuthCookies:RefreshTokenPath"] = "/";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("RefreshTokenPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsLocalhostAuthCookieDomain()
    {
        var configuration = BuildValidProductionConfiguration();
        configuration["AuthCookies:Domain"] = "localhost";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("AuthCookies:Domain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateProductionConfiguration_RejectsLogOnlyEmailDelivery()
    {
        var configuration = BuildValidProductionConfiguration();
        configuration["EmailDelivery:ProviderName"] = "LogOnly";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityConfigurationValidator.ValidateProductionConfiguration(configuration));

        Assert.Contains("EmailDelivery:ProviderName", exception.Message, StringComparison.Ordinal);
    }

    private static IConfigurationRoot BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static IConfigurationRoot BuildValidProductionConfiguration() =>
        BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db.internal;Port=5432;Database=swyftly;Username=swyftly_app;Password=ProdDbValueForTests2026!",
            ["Jwt:SigningKey"] = "ProdJwtSigningKeyValueForSwyftly2026!!",
            ["PaymentProvider:ProviderName"] = "PayFast",
            ["PayFast:MerchantId"] = "100001",
            ["PayFast:MerchantKey"] = "prod-merchant-key",
            ["PayFast:Passphrase"] = "prod-payfast-passphrase",
            ["PayFast:ProcessUrl"] = "https://www.payfast.co.za/eng/process",
            ["PayFast:NotifyUrl"] = "https://api.swyftly.example/api/payments/webhook/payfast",
            ["PayFast:CheckoutBridgeBaseUrl"] = "https://api.swyftly.example",
            ["PayFast:ValidateUrl"] = "https://www.payfast.co.za/eng/query/validate",
            ["PayFast:RequireRemoteValidation"] = "true",
            ["AuthCookies:Secure"] = "true",
            ["AuthCookies:SameSite"] = "Lax",
            ["AuthCookies:RefreshTokenPath"] = "/api/auth",
            ["AuthCookies:CsrfPath"] = "/",
            ["AuthCookies:Domain"] = ".swyftly.example",
            ["EmailDelivery:ProviderName"] = "Smtp",
            ["EmailDelivery:FromAddress"] = "no-reply@swyftly.example",
            ["EmailDelivery:Smtp:Host"] = "smtp.swyftly.example",
            ["EmailDelivery:Smtp:Port"] = "587"
        });
}
