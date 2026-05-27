using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using Swyftly.Application.Abstractions;
using Swyftly.Application.Advertising;
using Swyftly.Application.Admin;
using Swyftly.Application.Ai;
using Swyftly.Application.Analytics;
using Swyftly.Application.Catalog;
using Swyftly.Application.Delivery;
using Swyftly.Application.Disputes;
using Swyftly.Application.Inventory;
using Swyftly.Application.Ledger;
using Swyftly.Application.Media;
using Swyftly.Application.Notifications;
using Swyftly.Application.Orders;
using Swyftly.Application.Payments;
using Swyftly.Application.Refunds;
using Swyftly.Application.Returns;
using Swyftly.Application.Search;
using Swyftly.Application.Sellers;
using Swyftly.Infrastructure.Admin;
using Swyftly.Infrastructure.Advertising;
using Swyftly.Infrastructure.Ai;
using Swyftly.Infrastructure.Analytics;
using Swyftly.Infrastructure.Carriers;
using Swyftly.Infrastructure.Delivery;
using Swyftly.Infrastructure.Disputes;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Inventory;
using Swyftly.Infrastructure.Ledger;
using Swyftly.Infrastructure.Notifications;
using Swyftly.Infrastructure.Orders;
using Swyftly.Infrastructure.Payments;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Refunds;
using Swyftly.Infrastructure.Returns;
using Swyftly.Infrastructure.Search;
using Swyftly.Infrastructure.Sellers;
using Swyftly.Infrastructure.Storage;

namespace Swyftly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionName);
            options.Issuer = section["Issuer"] ?? options.Issuer;
            options.Audience = section["Audience"] ?? options.Audience;
            options.SigningKey = section["SigningKey"] ?? options.SigningKey;

            if (int.TryParse(section["AccessTokenMinutes"], out var accessTokenMinutes))
            {
                options.AccessTokenMinutes = accessTokenMinutes;
            }

            if (int.TryParse(section["RefreshTokenDays"], out var refreshTokenDays))
            {
                options.RefreshTokenDays = refreshTokenDays;
            }
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        services.AddDbContext<SwyftlyDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SwyftlyDbContext>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuditLogService, EfAuditLogService>();
        services.AddScoped<IProductSearchIndexer, ProductSearchIndexer>();
        services.AddSingleton<ISearchIndexService, LocalSearchIndexService>();
        services.Configure<ImageStorageOptions>(options =>
        {
            var section = configuration.GetSection(ImageStorageOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            options.LocalRootPath = section["LocalRootPath"] ?? options.LocalRootPath;
            options.PublicBasePath = section["PublicBasePath"] ?? options.PublicBasePath;
            if (long.TryParse(section["MaxFileBytes"], out var maxFileBytes))
            {
                options.MaxFileBytes = maxFileBytes;
            }

            if (int.TryParse(section["MaxPixelWidth"], out var maxPixelWidth))
            {
                options.MaxPixelWidth = maxPixelWidth;
            }

            if (int.TryParse(section["MaxPixelHeight"], out var maxPixelHeight))
            {
                options.MaxPixelHeight = maxPixelHeight;
            }

            if (int.TryParse(section["VariantWebpQuality"], out var variantWebpQuality))
            {
                options.VariantWebpQuality = variantWebpQuality;
            }

            var allowedTypes = section.GetSection("AllowedContentTypes")
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
            if (allowedTypes.Length > 0)
            {
                options.AllowedContentTypes = allowedTypes;
            }

            var s3 = section.GetSection("S3");
            options.S3.BucketName = s3["BucketName"] ?? options.S3.BucketName;
            options.S3.Region = s3["Region"] ?? options.S3.Region;
            options.S3.ServiceUrl = s3["ServiceUrl"] ?? options.S3.ServiceUrl;
            options.S3.PublicBaseUrl = s3["PublicBaseUrl"] ?? options.S3.PublicBaseUrl;
            options.S3.KeyPrefix = s3["KeyPrefix"] ?? options.S3.KeyPrefix;
            options.S3.AccessKeyId = s3["AccessKeyId"] ?? options.S3.AccessKeyId;
            options.S3.SecretAccessKey = s3["SecretAccessKey"] ?? options.S3.SecretAccessKey;
            options.S3.CacheControl = s3["CacheControl"] ?? options.S3.CacheControl;
            if (bool.TryParse(s3["ForcePathStyle"], out var forcePathStyle))
            {
                options.S3.ForcePathStyle = forcePathStyle;
            }
        });
        services.Configure<MediaScanningOptions>(options =>
        {
            var section = configuration.GetSection(MediaScanningOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            if (bool.TryParse(section["RequireExternalScannerInProduction"], out var requireExternalScanner))
            {
                options.RequireExternalScannerInProduction = requireExternalScanner;
            }
        });
        services.Configure<MediaCleanupOptions>(options =>
        {
            var section = configuration.GetSection(MediaCleanupOptions.SectionName);
            if (int.TryParse(section["GracePeriodHours"], out var gracePeriodHours))
            {
                options.GracePeriodHours = gracePeriodHours;
            }

            if (int.TryParse(section["BatchSize"], out var batchSize))
            {
                options.BatchSize = batchSize;
            }
        });
        services.Configure<SellerVerificationEvidenceOptions>(options =>
        {
            var section = configuration.GetSection(SellerVerificationEvidenceOptions.SectionName);
            options.LocalRootPath = section["LocalRootPath"] ?? options.LocalRootPath;
            if (long.TryParse(section["MaxFileBytes"], out var maxFileBytes))
            {
                options.MaxFileBytes = maxFileBytes;
            }

            if (int.TryParse(section["MaxActiveFilesPerSeller"], out var maxActiveFiles))
            {
                options.MaxActiveFilesPerSeller = maxActiveFiles;
            }

            var allowedTypes = section.GetSection("AllowedContentTypes")
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
            if (allowedTypes.Length > 0)
            {
                options.AllowedContentTypes = allowedTypes;
            }
        });
        services.Configure<EmailDeliveryOptions>(options =>
        {
            var section = configuration.GetSection(EmailDeliveryOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            options.FromAddress = section["FromAddress"] ?? options.FromAddress;
            options.FromName = section["FromName"] ?? options.FromName;
            options.AppBaseUrl = section["AppBaseUrl"] ?? options.AppBaseUrl;
            if (int.TryParse(section["BatchSize"], out var batchSize))
            {
                options.BatchSize = batchSize;
            }

            if (int.TryParse(section["MaxAttempts"], out var maxAttempts))
            {
                options.MaxAttempts = maxAttempts;
            }

            if (int.TryParse(section["RetryMinutes"], out var retryMinutes))
            {
                options.RetryMinutes = retryMinutes;
            }

            var smtp = section.GetSection("Smtp");
            options.Smtp.Host = smtp["Host"] ?? options.Smtp.Host;
            if (int.TryParse(smtp["Port"], out var smtpPort))
            {
                options.Smtp.Port = smtpPort;
            }

            options.Smtp.Username = smtp["Username"] ?? options.Smtp.Username;
            options.Smtp.Password = smtp["Password"] ?? options.Smtp.Password;
            if (bool.TryParse(smtp["EnableSsl"], out var enableSsl))
            {
                options.Smtp.EnableSsl = enableSsl;
            }
        });
        services.Configure<CarrierProviderOptions>(options =>
        {
            var section = configuration.GetSection(CarrierProviderOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;

            var fake = section.GetSection("Fake");
            options.Fake.TrackingBaseUrl = fake["TrackingBaseUrl"] ?? options.Fake.TrackingBaseUrl;
            options.Fake.LabelBaseUrl = fake["LabelBaseUrl"] ?? options.Fake.LabelBaseUrl;
        });
        services.Configure<CarrierTrackingOptions>(options =>
        {
            var section = configuration.GetSection(CarrierTrackingOptions.SectionName);
            if (int.TryParse(section["BatchSize"], out var batchSize))
            {
                options.BatchSize = batchSize;
            }

            if (int.TryParse(section["SyncIntervalMinutes"], out var syncIntervalMinutes))
            {
                options.SyncIntervalMinutes = syncIntervalMinutes;
            }
        });
        services.AddSingleton<IImageStorageProvider>(serviceProvider =>
        {
            var imageOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImageStorageOptions>>();
            return string.Equals(imageOptions.Value.ProviderName, "S3", StringComparison.OrdinalIgnoreCase)
                ? new S3ImageStorageProvider(imageOptions)
                : new LocalImageStorageProvider(imageOptions);
        });
        services.AddSingleton<IMediaMalwareScanner, TrustLocalCleanMediaMalwareScanner>();
        services.AddSingleton<MediaImageProcessor>();
        services.AddScoped<IProductMediaUploadService, ProductMediaUploadService>();
        services.AddScoped<IMediaCleanupService, EfMediaCleanupService>();
        services.AddScoped<ISellerVerificationEvidenceStorage, LocalSellerVerificationEvidenceStorage>();
        services.AddSingleton<ProductModerationService>();
        services.AddSingleton<AiPromptBuilder>();
        services.AddSingleton<AiSuggestionValidator>();
        services.AddScoped<AiUsageLogger>();
        services.AddScoped<IAiListingAssistantService, AiListingAssistantService>();
        services.AddSingleton<IAiProviderClient, FakeAiProviderClient>();
        services.AddScoped<IAiShoppingIntentService, AiShoppingIntentService>();
        services.AddSingleton<IAiShoppingIntentProvider, FakeAiShoppingIntentProvider>();
        services.AddScoped<IAiVisualSearchService, AiVisualSearchService>();
        services.AddSingleton<IAiVisionProvider, FakeAiVisionProvider>();
        services.AddScoped<IProductEmbeddingGenerator, ProductEmbeddingGenerator>();
        services.AddSingleton<IAiEmbeddingService, FakeAiEmbeddingService>();
        services.AddScoped<IInventoryReservationService, EfInventoryReservationService>();
        services.AddScoped<IAddressVerificationService, LocalRulesAddressVerificationService>();
        services.AddScoped<IOrderCreationService, EfOrderCreationService>();
        services.AddScoped<IOrderFulfillmentService, EfOrderFulfillmentService>();
        services.AddScoped<ManualCarrierProvider>();
        services.AddScoped<FakeCarrierProvider>();
        services.AddScoped<ICarrierProvider>(serviceProvider =>
        {
            var carrierOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CarrierProviderOptions>>().Value;
            return string.Equals(carrierOptions.ProviderName, FakeCarrierProvider.Name, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<FakeCarrierProvider>()
                : serviceProvider.GetRequiredService<ManualCarrierProvider>();
        });
        services.AddScoped<ICarrierTrackingSyncService, EfCarrierTrackingSyncService>();
        services.AddScoped<IReturnWorkflowService, EfReturnWorkflowService>();
        services.AddScoped<IRefundWorkflowService, EfRefundWorkflowService>();
        services.AddScoped<IDisputeWorkflowService, EfDisputeWorkflowService>();
        services.AddScoped<INotificationRealtimePublisher, NoOpNotificationRealtimePublisher>();
        services.AddScoped<INotificationService, EfNotificationService>();
        services.AddScoped<INotificationEmailDeliveryService, EfNotificationEmailDeliveryService>();
        services.AddScoped<LogOnlyEmailDeliveryProvider>();
        services.AddScoped<SmtpEmailDeliveryProvider>();
        services.AddScoped<IEmailDeliveryProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailDeliveryOptions>>().Value;
            return string.Equals(options.ProviderName, SmtpEmailDeliveryProvider.Name, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<SmtpEmailDeliveryProvider>()
                : serviceProvider.GetRequiredService<LogOnlyEmailDeliveryProvider>();
        });
        services.AddScoped<IAdCampaignEligibilityService, AdCampaignEligibilityService>();
        services.AddScoped<IAdTrackingService, EfAdTrackingService>();
        services.AddScoped<IStorefrontAnalyticsService, EfStorefrontAnalyticsService>();
        services.AddScoped<ISellerScheduledReportService, EfSellerScheduledReportService>();
        services.Configure<PaymentProviderOptions>(options =>
        {
            var section = configuration.GetSection(PaymentProviderOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            options.DefaultCurrency = section["DefaultCurrency"] ?? options.DefaultCurrency;
            options.SuccessRedirectUrl = section["SuccessRedirectUrl"] ?? options.SuccessRedirectUrl;
            options.FailureRedirectUrl = section["FailureRedirectUrl"] ?? options.FailureRedirectUrl;
            options.WebhookSigningSecret = section["WebhookSigningSecret"] ?? options.WebhookSigningSecret;
            options.FakeOutcome = section["FakeOutcome"] ?? options.FakeOutcome;
        });
        services.Configure<PaymentWebhookPayloadRetentionOptions>(options =>
        {
            var section = configuration.GetSection(PaymentWebhookPayloadRetentionOptions.SectionName);
            if (bool.TryParse(section["Enabled"], out var enabled))
            {
                options.Enabled = enabled;
            }

            if (int.TryParse(section["RetentionDays"], out var retentionDays))
            {
                options.RetentionDays = retentionDays;
            }

            if (int.TryParse(section["BatchSize"], out var batchSize))
            {
                options.BatchSize = batchSize;
            }
        });
        services.Configure<PayFastOptions>(options =>
        {
            var section = configuration.GetSection(PayFastOptions.SectionName);
            options.MerchantId = section["MerchantId"] ?? options.MerchantId;
            options.MerchantKey = section["MerchantKey"] ?? options.MerchantKey;
            options.Passphrase = section["Passphrase"] ?? options.Passphrase;
            options.ProcessUrl = section["ProcessUrl"] ?? options.ProcessUrl;
            options.ValidateUrl = section["ValidateUrl"] ?? options.ValidateUrl;
            options.NotifyUrl = section["NotifyUrl"] ?? options.NotifyUrl;
            options.CheckoutBridgeBaseUrl = section["CheckoutBridgeBaseUrl"] ?? options.CheckoutBridgeBaseUrl;

            if (bool.TryParse(section["RequireRemoteValidation"], out var requireRemoteValidation))
            {
                options.RequireRemoteValidation = requireRemoteValidation;
            }
        });
        services.Configure<LedgerOptions>(options =>
        {
            var section = configuration.GetSection(LedgerOptions.SectionName);
            if (decimal.TryParse(section["PlatformCommissionRatePercent"], out var commissionRate))
            {
                options.PlatformCommissionRatePercent = commissionRate;
            }

            if (decimal.TryParse(section["PaymentProviderFeeRatePercent"], out var providerFeeRate))
            {
                options.PaymentProviderFeeRatePercent = providerFeeRate;
            }

            if (decimal.TryParse(section["PaymentProviderFixedFee"], out var providerFixedFee))
            {
                options.PaymentProviderFixedFee = providerFixedFee;
            }
        });
        services.Configure<PayoutProviderOptions>(options =>
        {
            var section = configuration.GetSection(PayoutProviderOptions.SectionName);
            options.ProviderName = section["ProviderName"] ?? options.ProviderName;
            options.FakeOutcome = section["FakeOutcome"] ?? options.FakeOutcome;
        });
        services.AddScoped<FakePaymentProvider>();
        services.AddSingleton<HttpClient>();
        services.AddScoped<PayFastPaymentProvider>();
        services.AddScoped<PayFastCheckoutFormBuilder>();
        services.AddScoped<IPaymentProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentProviderOptions>>().Value;
            return string.Equals(options.ProviderName, PayFastPaymentProvider.Name, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<PayFastPaymentProvider>()
                : serviceProvider.GetRequiredService<FakePaymentProvider>();
        });
        services.AddScoped<IPayoutProvider, FakePayoutProvider>();
        services.AddScoped<IPaymentInitiationService, PaymentInitiationService>();
        services.AddScoped<IPaymentService, EfPaymentService>();
        services.AddScoped<IPaymentWebhookPayloadRetentionService, EfPaymentWebhookPayloadRetentionService>();
        services.AddScoped<ILedgerService, EfLedgerService>();
        services.AddScoped<IPayoutAdministrationService, EfPayoutAdministrationService>();

        return services;
    }
}
