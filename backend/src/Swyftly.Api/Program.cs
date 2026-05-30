using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swyftly.Api.Admin;
using Swyftly.Api.Advertising;
using Swyftly.Api.Ai;
using Swyftly.Api.Analytics;
using Swyftly.Api.Authentication;
using Swyftly.Api.Buyers;
using Swyftly.Api.Carts;
using Swyftly.Api.Catalog;
using Swyftly.Api.Disputes;
using Swyftly.Api.Notifications;
using Swyftly.Api.Observability;
using Swyftly.Api.Orders;
using Swyftly.Api.Payments;
using Swyftly.Api.Payouts;
using Swyftly.Api.Refunds;
using Swyftly.Api.Returns;
using Swyftly.Api.Security;
using Swyftly.Api.Sellers;
using Swyftly.Api.Support;
using Swyftly.Application.Abstractions;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Infrastructure;
using Swyftly.Infrastructure.Identity;
using Swyftly.Infrastructure.Persistence;
using Swyftly.Infrastructure.Storage;
using System.Text;
using System.Threading.RateLimiting;

const int ReadinessTimeoutSeconds = 5;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

var configuredFrontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
var allowedFrontendOrigins = new[]
    {
        "http://localhost:4200",
        "https://localhost:4200",
        "http://localhost:4201",
        "https://localhost:4201",
        "https://swyftly.co.za"
    }
    .Concat(configuredFrontendOrigins)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedFrontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SwyftlyMetrics>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();

var rateLimitOptions = builder.Configuration
    .GetSection(SwyftlyRateLimitOptions.SectionName)
    .Get<SwyftlyRateLimitOptions>() ?? new SwyftlyRateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "RateLimit.Exceeded",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many requests. Please wait before trying again."
        }, cancellationToken);
    };

    options.AddPolicy(SwyftlyRateLimitPolicies.Auth, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.Auth));
    options.AddPolicy(SwyftlyRateLimitPolicies.Ai, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.Ai));
    options.AddPolicy(SwyftlyRateLimitPolicies.ProductWrite, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.ProductWrite));
    options.AddPolicy(SwyftlyRateLimitPolicies.Payment, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.Payment));
    options.AddPolicy(SwyftlyRateLimitPolicies.Webhook, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.Webhook));
    options.AddPolicy(SwyftlyRateLimitPolicies.AdImpression, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.AdImpression));
    options.AddPolicy(SwyftlyRateLimitPolicies.AdClick, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.AdClick));
    options.AddPolicy(SwyftlyRateLimitPolicies.StorefrontAnalytics, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.StorefrontAnalytics));
    options.AddPolicy(SwyftlyRateLimitPolicies.Search, httpContext => CreateFixedWindowLimiter(httpContext, rateLimitOptions.Search));
});

SecurityConfigurationValidator.Validate(builder.Configuration, builder.Environment);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SwyftlyPolicies.BuyerOnly, policy => policy.RequireRole(SwyftlyRoles.Buyer));
    options.AddPolicy(SwyftlyPolicies.SellerOnly, policy => policy.RequireRole(SwyftlyRoles.Seller));
    options.AddPolicy(SwyftlyPolicies.BuyerOrSeller, policy => policy.RequireRole(SwyftlyRoles.Buyer, SwyftlyRoles.Seller));
    options.AddPolicy(SwyftlyPolicies.AdminOnly, policy => policy.RequireRole(SwyftlyRoles.Admin, SwyftlyRoles.SuperAdmin));
    options.AddPolicy(SwyftlyPolicies.SuperAdminOnly, policy => policy.RequireRole(SwyftlyRoles.SuperAdmin));
    options.AddPolicy(SwyftlyPolicies.SupportAgentOnly, policy => policy.RequireRole(
        SwyftlyRoles.SupportAgent,
        SwyftlyRoles.Admin,
        SwyftlyRoles.SuperAdmin));
    options.AddPolicy(SwyftlyPolicies.FinanceRead, policy => policy.RequireRole(
        SwyftlyRoles.Admin,
        SwyftlyRoles.SuperAdmin,
        SwyftlyRoles.FinanceOperator,
        SwyftlyRoles.FinanceApprover));
    options.AddPolicy(SwyftlyPolicies.FinanceOperate, policy => policy.RequireRole(
        SwyftlyRoles.FinanceOperator,
        SwyftlyRoles.SuperAdmin));
    options.AddPolicy(SwyftlyPolicies.FinanceApprove, policy => policy.RequireRole(
        SwyftlyRoles.FinanceApprover,
        SwyftlyRoles.SuperAdmin));
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<SwyftlyDbContext>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "database" })
    .AddCheck(
        "search-placeholder",
        () => HealthCheckResult.Healthy("Search provider placeholder is available."),
        tags: new[] { "ready", "search" })
    .AddCheck<ImageStorageHealthCheck>(
        name: "image-storage",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "storage" })
    .AddCheck<PaymentProviderHealthCheck>(
        name: "payment-provider",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "payments" })
    .AddCheck<EmailDeliveryHealthCheck>(
        name: "email-delivery",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "notifications" });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Swyftly API",
        Version = "v1",
        Description = "Foundation API for the Swyftly marketplace."
    });

    options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", "."));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT access token."
    });
});

var app = builder.Build();


var imageStorageOptions = builder.Configuration
    .GetSection(ImageStorageOptions.SectionName)
    .Get<ImageStorageOptions>() ?? new ImageStorageOptions();
var useLocalImageStorage = !string.Equals(imageStorageOptions.ProviderName, "S3", StringComparison.OrdinalIgnoreCase);
var imageStorageRoot = imageStorageOptions.LocalRootPath;
if (useLocalImageStorage && !Path.IsPathRooted(imageStorageRoot))
{
    imageStorageRoot = Path.Combine(app.Environment.ContentRootPath, imageStorageRoot);
}

if (useLocalImageStorage)
{
    imageStorageRoot = Path.GetFullPath(imageStorageRoot);
    Directory.CreateDirectory(imageStorageRoot);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Swyftly API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseRouting();
if (useLocalImageStorage)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imageStorageRoot),
        RequestPath = imageStorageOptions.PublicBasePath.StartsWith('/')
            ? imageStorageOptions.PublicBasePath.TrimEnd('/')
            : $"/{imageStorageOptions.PublicBasePath.TrimEnd('/')}"
    });
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors("Frontend");

// 1.Configure the Forwarded Headers Middleware
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// Security Note: By default, .NET only trusts requests coming from 'localhost' (127.0.0.1).
// Since Cloudflare is sending the requests, we clear the KnownProxies and KnownNetworks
// restrictions so it accepts the headers from Cloudflare's edge IPs.
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownNetworks.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await IdentitySeeder.TrySeedIdentityRolesAsync(app.Services, app.Logger);
await CatalogSeeder.TrySeedCatalogAsync(app.Services, app.Logger);

app.MapGet("/health", () => new HealthResponse(
    "Healthy",
    "Swyftly.Api",
    DateTimeOffset.UtcNow))
    .WithName("GetHealth")
    .WithSummary("Returns the API health status.")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/health/ready", async (HealthCheckService healthCheckService) =>
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ReadinessTimeoutSeconds));
    HealthReport report;

    try
    {
        report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            timeout.Token);
    }
    catch (OperationCanceledException)
    {
        var timeoutResponse = new ReadinessHealthResponse(
            HealthStatus.Unhealthy.ToString(),
            "Swyftly.Api",
            DateTimeOffset.UtcNow,
            ReadinessTimeoutSeconds * 1000,
            new Dictionary<string, ReadinessCheckResponse>
            {
                ["postgresql"] = new(
                    HealthStatus.Unhealthy.ToString(),
                    "Readiness check timed out.",
                    null,
                    ReadinessTimeoutSeconds * 1000)
            });

        return Results.Json(timeoutResponse, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var checks = report.Entries.ToDictionary(
        entry => entry.Key,
        entry => new ReadinessCheckResponse(
            entry.Value.Status.ToString(),
            entry.Value.Description,
            entry.Value.Exception?.Message,
            Math.Round(entry.Value.Duration.TotalMilliseconds, 2)));

    var response = new ReadinessHealthResponse(
        report.Status.ToString(),
        "Swyftly.Api",
        DateTimeOffset.UtcNow,
        Math.Round(report.TotalDuration.TotalMilliseconds, 2),
        checks);

    var statusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    return Results.Json(response, statusCode: statusCode);
})
    .WithName("GetReadiness")
    .WithSummary("Returns readiness status for API dependencies.")
    .Produces<ReadinessHealthResponse>(StatusCodes.Status200OK)
    .Produces<ReadinessHealthResponse>(StatusCodes.Status503ServiceUnavailable);

app.MapAuthEndpoints();
app.MapSellerOnboardingEndpoints();
app.MapSellerVerificationEvidenceEndpoints();
app.MapSellerPayoutProfileChangeEndpoints();
app.MapAdminSellerEndpoints();
app.MapAdminProductEndpoints();
app.MapAdminQueueTriageEndpoints();
app.MapAdminModerationQueueEndpoints();
app.MapAdminReviewEndpoints();
app.MapAdminAuditLogEndpoints();
app.MapAdminDashboardEndpoints();
app.MapAdminMarketplaceReportEndpoints();
app.MapAdminAiUsageAnalyticsEndpoints();
app.MapAdminInventoryLedgerEndpoints();
app.MapAdminCategoryEndpoints();
app.MapAdminPickupPointEndpoints();
app.MapAdminOrderPaymentEndpoints();
app.MapSellerCatalogEndpoints();
app.MapSellerProductEndpoints();
app.MapSellerInventoryEndpoints();
app.MapSellerDeliveryMethodEndpoints();
app.MapSellerStorePolicyEndpoints();
app.MapSellerNotificationEndpoints();
app.MapSellerDashboardEndpoints();
app.MapPublicProductEndpoints();
app.MapBuyerSettingsEndpoints();
app.MapBuyerEngagementEndpoints();
app.MapBuyerGrowthEventEndpoints();
app.MapBuyerAiDiscoveryEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapPaymentEndpoints();
app.MapPayoutEndpoints();
app.MapReturnEndpoints();
app.MapRefundEndpoints();
app.MapDisputeEndpoints();
app.MapSupportTicketEndpoints();
app.MapSellerAdCampaignEndpoints();
app.MapAdminAdCampaignEndpoints();
app.MapAdTrackingEndpoints();
app.MapStorefrontAnalyticsEndpoints();
app.MapSellerAnalyticsEndpoints();
app.MapBuyerAiShoppingAssistantEndpoints();
app.MapBuyerVisualSearchEndpoints();
app.MapHub<NotificationHub>("/hubs/notifications")
    .RequireAuthorization(SwyftlyPolicies.BuyerOrSeller);

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();

static RateLimitPartition<string> CreateFixedWindowLimiter(
    HttpContext httpContext,
    RateLimitPolicyOptions options)
{
    var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
        ? $"user:{httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown"}"
        : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = options.PermitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromSeconds(options.WindowSeconds)
        });
}

public partial class Program;

public sealed record HealthResponse(
    string Status,
    string ApplicationName,
    DateTimeOffset TimestampUtc);

public sealed record ReadinessHealthResponse(
    string Status,
    string ApplicationName,
    DateTimeOffset TimestampUtc,
    double TotalDurationMilliseconds,
    IReadOnlyDictionary<string, ReadinessCheckResponse> Checks);

public sealed record ReadinessCheckResponse(
    string Status,
    string? Description,
    string? Error,
    double DurationMilliseconds);
