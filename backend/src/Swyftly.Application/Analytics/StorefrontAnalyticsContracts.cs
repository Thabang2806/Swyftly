using Swyftly.Application.Common.Results;
using Swyftly.Domain.Sellers;

namespace Swyftly.Application.Analytics;

public interface IStorefrontAnalyticsService
{
    Task<Result<StorefrontFunnelEventResult>> RecordClientEventAsync(
        StorefrontFunnelEventRequest request,
        CancellationToken cancellationToken = default);

    Task RecordOrderCreatedAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task RecordOrderPaidAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontFunnelEventRequest(
    SellerFunnelEventType EventType,
    Guid? ProductId,
    Guid? CartId,
    string? SellerStoreSlug,
    Guid? BuyerId,
    string? AnonymousVisitorId,
    string? SourceRoute,
    string? IdempotencyKey,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? ReferrerHost,
    string? SourceCategory);

public sealed record StorefrontFunnelEventResult(
    bool Recorded,
    Guid? EventId,
    string Status);
