using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mabuntle.Application.Analytics;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Orders;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Analytics;

public sealed class EfBuyerGrowthOutcomeAttributionService(
    MabuntleDbContext dbContext,
    ILogger<EfBuyerGrowthOutcomeAttributionService> logger) : IBuyerGrowthOutcomeAttributionService
{
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromDays(7);

    public async Task RecordProductOpenedAsync(
        Guid buyerId,
        Guid productId,
        Guid sourceEventId,
        BuyerGrowthSourceTool sourceTool,
        BuyerGrowthConfidenceBand? confidenceBand,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RecordBestEffortAsync(
            "product-open",
            async () =>
            {
                if (buyerId == Guid.Empty || productId == Guid.Empty || sourceEventId == Guid.Empty)
                {
                    return;
                }

                if (await dbContext.BuyerGrowthOutcomes.AnyAsync(
                    outcome => outcome.SourceEventId == sourceEventId
                        && outcome.OutcomeType == BuyerGrowthOutcomeType.ProductOpened,
                    cancellationToken))
                {
                    return;
                }

                dbContext.BuyerGrowthOutcomes.Add(new BuyerGrowthOutcome(
                    buyerId,
                    BuyerGrowthOutcomeType.ProductOpened,
                    sourceTool,
                    occurredAtUtc,
                    occurredAtUtc.Subtract(AttributionWindow),
                    (int)AttributionWindow.TotalMinutes,
                    sourceEventId,
                    productId,
                    confidenceBand: confidenceBand));
                await dbContext.SaveChangesAsync(cancellationToken);
            });
    }

    public async Task RecordProductAddedToCartAsync(
        Guid buyerId,
        Guid productId,
        Guid cartId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RecordBestEffortAsync(
            "product-add-to-cart",
            async () =>
            {
                var attribution = await ResolveProductAttributionAsync(buyerId, productId, occurredAtUtc, cancellationToken);
                if (attribution is null)
                {
                    return;
                }

                if (await ExistsAsync(
                    buyerId,
                    BuyerGrowthOutcomeType.ProductAddedToCart,
                    attribution.SourceTool,
                    productId,
                    cartId,
                    orderId: null,
                    cancellationToken))
                {
                    return;
                }

                dbContext.BuyerGrowthOutcomes.Add(new BuyerGrowthOutcome(
                    buyerId,
                    BuyerGrowthOutcomeType.ProductAddedToCart,
                    attribution.SourceTool,
                    occurredAtUtc,
                    occurredAtUtc.Subtract(AttributionWindow),
                    (int)AttributionWindow.TotalMinutes,
                    attribution.SourceEventId,
                    productId,
                    cartId,
                    confidenceBand: attribution.ConfidenceBand));
                await dbContext.SaveChangesAsync(cancellationToken);
            });
    }

    public async Task RecordCheckoutStartedAsync(
        Guid buyerId,
        Guid cartId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RecordBestEffortAsync(
            "checkout-started",
            async () =>
            {
                var productIds = await dbContext.Carts
                    .AsNoTracking()
                    .Where(cart => cart.Id == cartId && cart.BuyerId == buyerId)
                    .SelectMany(cart => cart.Items.Select(item => item.ProductId))
                    .Distinct()
                    .ToArrayAsync(cancellationToken);
                var attribution = await ResolveCartAttributionAsync(buyerId, productIds, occurredAtUtc, cancellationToken);
                if (attribution is null)
                {
                    return;
                }

                if (await ExistsAsync(
                    buyerId,
                    BuyerGrowthOutcomeType.CheckoutStarted,
                    attribution.SourceTool,
                    attribution.ProductId,
                    cartId,
                    orderId: null,
                    cancellationToken))
                {
                    return;
                }

                dbContext.BuyerGrowthOutcomes.Add(new BuyerGrowthOutcome(
                    buyerId,
                    BuyerGrowthOutcomeType.CheckoutStarted,
                    attribution.SourceTool,
                    occurredAtUtc,
                    occurredAtUtc.Subtract(AttributionWindow),
                    (int)AttributionWindow.TotalMinutes,
                    attribution.SourceEventId,
                    attribution.ProductId,
                    cartId,
                    confidenceBand: attribution.ConfidenceBand));
                await dbContext.SaveChangesAsync(cancellationToken);
            });
    }

    public async Task RecordOrderCreatedAsync(
        Guid orderId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RecordOrderOutcomeAsync(BuyerGrowthOutcomeType.OrderCreated, orderId, occurredAtUtc, cancellationToken);
    }

    public async Task RecordOrderPaidAsync(
        Guid orderId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RecordOrderOutcomeAsync(BuyerGrowthOutcomeType.OrderPaid, orderId, occurredAtUtc, cancellationToken);
    }

    private async Task RecordOrderOutcomeAsync(
        BuyerGrowthOutcomeType outcomeType,
        Guid orderId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await RecordBestEffortAsync(
            outcomeType.ToString(),
            async () =>
            {
                var order = await dbContext.Orders
                    .AsNoTracking()
                    .Include(candidate => candidate.Items)
                    .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
                if (order is null)
                {
                    return;
                }

                var productIds = order.Items.Select(item => item.ProductId).Distinct().ToArray();
                foreach (var productId in productIds)
                {
                    var attribution = await ResolveProductAttributionAsync(order.BuyerId, productId, occurredAtUtc, cancellationToken);
                    if (attribution is null)
                    {
                        continue;
                    }

                    if (await ExistsAsync(
                        order.BuyerId,
                        outcomeType,
                        attribution.SourceTool,
                        productId,
                        order.CartId,
                        order.Id,
                        cancellationToken))
                    {
                        continue;
                    }

                    dbContext.BuyerGrowthOutcomes.Add(new BuyerGrowthOutcome(
                        order.BuyerId,
                        outcomeType,
                        attribution.SourceTool,
                        occurredAtUtc,
                        occurredAtUtc.Subtract(AttributionWindow),
                        (int)AttributionWindow.TotalMinutes,
                        attribution.SourceEventId,
                        productId,
                        order.CartId,
                        order.Id,
                        attribution.ConfidenceBand));
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            });
    }

    private async Task<ResolvedAttribution?> ResolveCartAttributionAsync(
        Guid buyerId,
        IReadOnlyCollection<Guid> productIds,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        foreach (var productId in productIds)
        {
            var attribution = await ResolveProductAttributionAsync(buyerId, productId, occurredAtUtc, cancellationToken);
            if (attribution is not null)
            {
                return attribution;
            }
        }

        return await ResolveLatestShopHandoffAsync(buyerId, occurredAtUtc, cancellationToken);
    }

    private async Task<ResolvedAttribution?> ResolveProductAttributionAsync(
        Guid buyerId,
        Guid productId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (buyerId == Guid.Empty || productId == Guid.Empty)
        {
            return null;
        }

        var windowStart = occurredAtUtc.Subtract(AttributionWindow);
        var productOpen = await dbContext.BuyerGrowthEvents
            .AsNoTracking()
            .Where(growthEvent => growthEvent.BuyerId == buyerId
                && growthEvent.ProductId == productId
                && growthEvent.OccurredAtUtc >= windowStart
                && growthEvent.OccurredAtUtc <= occurredAtUtc
                && (growthEvent.EventType == BuyerGrowthEventType.AssistantProductOpened
                    || growthEvent.EventType == BuyerGrowthEventType.VisualProductOpened))
            .OrderByDescending(growthEvent => growthEvent.OccurredAtUtc)
            .Select(growthEvent => new ResolvedAttribution(
                growthEvent.Id,
                growthEvent.SourceTool,
                growthEvent.ConfidenceBand,
                growthEvent.ProductId))
            .FirstOrDefaultAsync(cancellationToken);

        return productOpen ?? await ResolveLatestShopHandoffAsync(buyerId, occurredAtUtc, cancellationToken);
    }

    private async Task<ResolvedAttribution?> ResolveLatestShopHandoffAsync(
        Guid buyerId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var windowStart = occurredAtUtc.Subtract(AttributionWindow);
        return await dbContext.BuyerGrowthEvents
            .AsNoTracking()
            .Where(growthEvent => growthEvent.BuyerId == buyerId
                && growthEvent.OccurredAtUtc >= windowStart
                && growthEvent.OccurredAtUtc <= occurredAtUtc
                && (growthEvent.EventType == BuyerGrowthEventType.AssistantShopHandoff
                    || growthEvent.EventType == BuyerGrowthEventType.VisualShopHandoff))
            .OrderByDescending(growthEvent => growthEvent.OccurredAtUtc)
            .Select(growthEvent => new ResolvedAttribution(
                growthEvent.Id,
                growthEvent.SourceTool,
                growthEvent.ConfidenceBand,
                growthEvent.ProductId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> ExistsAsync(
        Guid buyerId,
        BuyerGrowthOutcomeType outcomeType,
        BuyerGrowthSourceTool sourceTool,
        Guid? productId,
        Guid? cartId,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        return await dbContext.BuyerGrowthOutcomes.AnyAsync(
            outcome => outcome.BuyerId == buyerId
                && outcome.OutcomeType == outcomeType
                && outcome.SourceTool == sourceTool
                && outcome.ProductId == productId
                && outcome.CartId == cartId
                && outcome.OrderId == orderId,
            cancellationToken);
    }

    private async Task RecordBestEffortAsync(string operation, Func<Task> recordAsync)
    {
        try
        {
            await recordAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to record buyer growth attribution outcome for {Operation}.", operation);
        }
    }

    private sealed record ResolvedAttribution(
        Guid SourceEventId,
        BuyerGrowthSourceTool SourceTool,
        BuyerGrowthConfidenceBand? ConfidenceBand,
        Guid? ProductId);
}
