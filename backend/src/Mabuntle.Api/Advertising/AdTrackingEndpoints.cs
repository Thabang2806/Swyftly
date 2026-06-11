using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Security;
using Mabuntle.Application.Advertising;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Advertising;

public static class AdTrackingEndpoints
{
    public static IEndpointRouteBuilder MapAdTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/ads")
            .WithTags("Ad Tracking")
            .AllowAnonymous();

        publicGroup.MapPost("/impressions", RecordImpressionAsync)
            .WithName("RecordAdImpression")
            .WithSummary("Records an ad impression when a promoted product is rendered.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.AdImpression)
            .Produces<AdTrackingResult>(StatusCodes.Status202Accepted);

        publicGroup.MapPost("/clicks", RecordClickAsync)
            .WithName("RecordAdClick")
            .WithSummary("Records an ad click when a promoted product is selected.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.AdClick)
            .Produces<AdTrackingResult>(StatusCodes.Status202Accepted);

        app.MapGet("/api/seller/ad-campaigns/{id:guid}/metrics", GetSellerCampaignMetricsAsync)
            .WithTags("Seller Ad Campaigns")
            .WithName("GetSellerAdCampaignMetrics")
            .WithSummary("Returns seller-owned ad campaign metrics.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<AdCampaignMetricsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordImpressionAsync(
        TrackAdImpressionApiRequest request,
        HttpContext httpContext,
        IAdTrackingService adTrackingService,
        CancellationToken cancellationToken)
    {
        var result = await adTrackingService.RecordImpressionAsync(
            new TrackAdImpressionRequest(
                request.AdCampaignId,
                request.ProductId,
                request.Placement,
                ResolveVisitorId(request.AnonymousVisitorId, httpContext)),
            cancellationToken);

        return HttpResults.Accepted(value: result);
    }

    private static async Task<IResult> RecordClickAsync(
        TrackAdClickApiRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAdTrackingService adTrackingService,
        CancellationToken cancellationToken)
    {
        var buyer = principal.Identity?.IsAuthenticated == true
            ? await GetCurrentBuyerAsync(principal, dbContext, cancellationToken)
            : null;

        var result = await adTrackingService.RecordClickAsync(
            new TrackAdClickRequest(
                request.AdCampaignId,
                request.ProductId,
                buyer?.Id,
                ResolveVisitorId(request.AnonymousVisitorId, httpContext)),
            cancellationToken);

        return HttpResults.Accepted(value: result);
    }

    private static async Task<IResult> GetSellerCampaignMetricsAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdTrackingService adTrackingService,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var metrics = await adTrackingService.GetCampaignMetricsAsync(seller.Id, id, cancellationToken);
        return metrics is null ? CampaignNotFound() : HttpResults.Ok(metrics);
    }

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "AdTracking.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult CampaignNotFound() =>
        HttpResults.Problem(
            title: "AdTracking.CampaignNotFound",
            detail: "Ad campaign was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static string? ResolveVisitorId(string? suppliedVisitorId, HttpContext httpContext)
    {
        var trimmed = suppliedVisitorId?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed.Length > 128 ? trimmed[..128] : trimmed;
        }

        var remoteAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(remoteAddress) && string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var fingerprintSource = $"{remoteAddress}|{userAgent}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        return $"fp_{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
    }
}

public sealed record TrackAdImpressionApiRequest(
    Guid AdCampaignId,
    Guid ProductId,
    string Placement,
    string? AnonymousVisitorId);

public sealed record TrackAdClickApiRequest(
    Guid AdCampaignId,
    Guid ProductId,
    string? AnonymousVisitorId);
