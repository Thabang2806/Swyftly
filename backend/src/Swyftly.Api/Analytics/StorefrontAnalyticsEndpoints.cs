using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swyftly.Api.Results;
using Swyftly.Api.Security;
using Swyftly.Application.Analytics;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Analytics;

public static class StorefrontAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapStorefrontAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics")
            .WithTags("Storefront Analytics")
            .AllowAnonymous();

        group.MapPost("/storefront-events", RecordStorefrontEventAsync)
            .WithName("RecordStorefrontFunnelEvent")
            .WithSummary("Records a first-party storefront funnel event for seller analytics.")
            .RequireRateLimiting(SwyftlyRateLimitPolicies.StorefrontAnalytics)
            .Produces<StorefrontFunnelEventResult>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordStorefrontEventAsync(
        StorefrontFunnelEventApiRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        IStorefrontAnalyticsService analyticsService,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SellerFunnelEventType>(request.EventType, ignoreCase: true, out var eventType)
            || !Enum.IsDefined(eventType))
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["eventType"] = ["eventType must be StorefrontViewed, ProductViewed, ProductAddedToCart, or CheckoutStarted."]
            });
        }

        var buyer = principal.Identity?.IsAuthenticated == true
            ? await GetCurrentBuyerAsync(principal, dbContext, cancellationToken)
            : null;

        var result = await analyticsService.RecordClientEventAsync(
            new StorefrontFunnelEventRequest(
                eventType,
                request.ProductId,
                request.CartId,
                request.SellerStoreSlug,
                buyer?.Id,
                request.AnonymousVisitorId,
                request.SourceRoute,
                request.IdempotencyKey,
                request.UtmSource,
                request.UtmMedium,
                request.UtmCampaign,
                request.ReferrerHost,
                request.SourceCategory),
            cancellationToken);

        return result.ToHttpResult(value => HttpResults.Accepted(value: value));
    }

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }
}

public sealed record StorefrontFunnelEventApiRequest(
    string EventType,
    Guid? ProductId,
    Guid? CartId,
    string? SellerStoreSlug,
    string? AnonymousVisitorId,
    string? SourceRoute,
    string? IdempotencyKey,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? ReferrerHost,
    string? SourceCategory);
