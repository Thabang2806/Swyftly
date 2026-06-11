namespace Mabuntle.Application.Advertising;

public interface IAdTrackingService
{
    Task<AdTrackingResult> RecordImpressionAsync(
        TrackAdImpressionRequest request,
        CancellationToken cancellationToken = default);

    Task<AdTrackingResult> RecordClickAsync(
        TrackAdClickRequest request,
        CancellationToken cancellationToken = default);

    Task AttributeOrderConversionsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<AdCampaignMetricsResponse?> GetCampaignMetricsAsync(
        Guid sellerId,
        Guid adCampaignId,
        CancellationToken cancellationToken = default);
}

public sealed record TrackAdImpressionRequest(
    Guid AdCampaignId,
    Guid ProductId,
    string Placement,
    string? AnonymousVisitorId);

public sealed record TrackAdClickRequest(
    Guid AdCampaignId,
    Guid ProductId,
    Guid? BuyerId,
    string? AnonymousVisitorId);

public sealed record AdTrackingResult(
    bool Recorded,
    Guid? EventId,
    string Status,
    string? Reason);

public sealed record AdCampaignMetricsResponse(
    Guid AdCampaignId,
    Guid SellerId,
    string Status,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    decimal ReturnOnAdSpend,
    string Currency);
