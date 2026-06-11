using Mabuntle.Domain.Buyers;

namespace Mabuntle.Application.Analytics;

public interface IBuyerGrowthOutcomeAttributionService
{
    Task RecordProductOpenedAsync(
        Guid buyerId,
        Guid productId,
        Guid sourceEventId,
        BuyerGrowthSourceTool sourceTool,
        BuyerGrowthConfidenceBand? confidenceBand,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordProductAddedToCartAsync(
        Guid buyerId,
        Guid productId,
        Guid cartId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordCheckoutStartedAsync(
        Guid buyerId,
        Guid cartId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordOrderCreatedAsync(
        Guid orderId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordOrderPaidAsync(
        Guid orderId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);
}
