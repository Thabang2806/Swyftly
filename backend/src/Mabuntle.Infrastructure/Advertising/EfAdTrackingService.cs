using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Advertising;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Advertising;

public sealed class EfAdTrackingService(
    MabuntleDbContext dbContext,
    TimeProvider timeProvider) : IAdTrackingService
{
    private static readonly TimeSpan ImpressionDedupeWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ClickDedupeWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AttributionWindow = TimeSpan.FromDays(7);

    public async Task<AdTrackingResult> RecordImpressionAsync(
        TrackAdImpressionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AdCampaignId == Guid.Empty || request.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(request.Placement))
        {
            return NotRecorded("InvalidRequest", "Campaign id, product id, and placement are required.");
        }

        var now = timeProvider.GetUtcNow();
        var trackable = await IsTrackableCampaignProductAsync(
            request.AdCampaignId,
            request.ProductId,
            now,
            cancellationToken);
        if (!trackable.Recorded)
        {
            return trackable;
        }

        var visitorId = TrimOrNull(request.AnonymousVisitorId, 128);
        var placement = request.Placement.Trim();
        if (visitorId is not null)
        {
            var dedupeAfter = now.Subtract(ImpressionDedupeWindow);
            var duplicate = await dbContext.AdImpressions.AnyAsync(
                impression => impression.AdCampaignId == request.AdCampaignId
                    && impression.ProductId == request.ProductId
                    && impression.Placement == placement
                    && impression.AnonymousVisitorId == visitorId
                    && impression.OccurredAtUtc >= dedupeAfter,
                cancellationToken);
            if (duplicate)
            {
                return NotRecorded("Duplicate", "A recent impression was already recorded for this visitor.");
            }
        }

        var impression = new AdImpression(
            request.AdCampaignId,
            request.ProductId,
            placement,
            visitorId,
            now);
        dbContext.AdImpressions.Add(impression);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Recorded(impression.Id, "ImpressionRecorded");
    }

    public async Task<AdTrackingResult> RecordClickAsync(
        TrackAdClickRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AdCampaignId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return NotRecorded("InvalidRequest", "Campaign id and product id are required.");
        }

        var now = timeProvider.GetUtcNow();
        var trackable = await IsTrackableCampaignProductAsync(
            request.AdCampaignId,
            request.ProductId,
            now,
            cancellationToken);
        if (!trackable.Recorded)
        {
            return trackable;
        }

        var buyerId = request.BuyerId == Guid.Empty ? null : request.BuyerId;
        var visitorId = TrimOrNull(request.AnonymousVisitorId, 128);
        if (buyerId.HasValue || visitorId is not null)
        {
            var dedupeAfter = now.Subtract(ClickDedupeWindow);
            var duplicate = await dbContext.AdClicks.AnyAsync(
                click => click.AdCampaignId == request.AdCampaignId
                    && click.ProductId == request.ProductId
                    && click.OccurredAtUtc >= dedupeAfter
                    && ((buyerId.HasValue && click.BuyerId == buyerId)
                        || (visitorId != null && click.AnonymousVisitorId == visitorId)),
                cancellationToken);
            if (duplicate)
            {
                return NotRecorded("Duplicate", "A recent click was already recorded for this buyer or visitor.");
            }
        }

        var budget = await dbContext.AdBudgets.SingleOrDefaultAsync(
            item => item.AdCampaignId == request.AdCampaignId,
            cancellationToken);
        if (budget is null)
        {
            return NotRecorded("BudgetMissing", "Campaign budget was not found.");
        }

        var chargeAmount = await GetAvailableClickChargeAsync(budget, now, cancellationToken);
        if (chargeAmount <= 0)
        {
            return NotRecorded("BudgetExhausted", "Campaign budget is exhausted.");
        }

        var click = new AdClick(request.AdCampaignId, request.ProductId, buyerId, visitorId, now);
        dbContext.AdClicks.Add(click);
        budget.AddSpend(chargeAmount, now);
        dbContext.AdCharges.Add(new AdCharge(request.AdCampaignId, click.Id, chargeAmount, budget.Currency, "Click", now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Recorded(click.Id, "ClickRecorded");
    }

    public async Task AttributeOrderConversionsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var clickAfter = now.Subtract(AttributionWindow);
        var productRevenue = order.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.LineTotal));

        foreach (var item in productRevenue)
        {
            var click = await dbContext.AdClicks
                .Where(click => click.BuyerId == order.BuyerId
                    && click.ProductId == item.Key
                    && click.OccurredAtUtc >= clickAfter
                    && !dbContext.AdConversions.Any(conversion => conversion.AdClickId == click.Id)
                    && !dbContext.AdConversions.Any(conversion => conversion.OrderId == order.Id && conversion.AdCampaignId == click.AdCampaignId))
                .OrderByDescending(click => click.OccurredAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (click is null)
            {
                continue;
            }

            var campaign = await dbContext.AdCampaigns.SingleOrDefaultAsync(
                campaign => campaign.Id == click.AdCampaignId,
                cancellationToken);
            if (campaign?.Status != AdCampaignStatus.Active)
            {
                continue;
            }

            dbContext.AdConversions.Add(new AdConversion(
                click.AdCampaignId,
                click.Id,
                order.Id,
                item.Value,
                "ZAR",
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdCampaignMetricsResponse?> GetCampaignMetricsAsync(
        Guid sellerId,
        Guid adCampaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.AdCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                campaign => campaign.Id == adCampaignId && campaign.SellerId == sellerId,
                cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var impressions = await dbContext.AdImpressions.CountAsync(
            impression => impression.AdCampaignId == campaign.Id,
            cancellationToken);
        var clicks = await dbContext.AdClicks.CountAsync(
            click => click.AdCampaignId == campaign.Id,
            cancellationToken);
        var spend = await dbContext.AdCharges
            .Where(charge => charge.AdCampaignId == campaign.Id)
            .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
        var conversions = await dbContext.AdConversions
            .Where(conversion => conversion.AdCampaignId == campaign.Id)
            .ToListAsync(cancellationToken);
        var revenue = conversions.Sum(conversion => conversion.RevenueAmount);
        var currency = await dbContext.AdBudgets
            .Where(budget => budget.AdCampaignId == campaign.Id)
            .Select(budget => budget.Currency)
            .SingleOrDefaultAsync(cancellationToken) ?? "ZAR";

        return new AdCampaignMetricsResponse(
            campaign.Id,
            campaign.SellerId,
            campaign.Status.ToString(),
            impressions,
            clicks,
            impressions == 0 ? 0 : decimal.Round((decimal)clicks / impressions, 4),
            spend,
            conversions.Select(conversion => conversion.OrderId).Distinct().Count(),
            revenue,
            spend == 0 ? 0 : decimal.Round(revenue / spend, 4),
            currency);
    }

    private async Task<AdTrackingResult> IsTrackableCampaignProductAsync(
        Guid adCampaignId,
        Guid productId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == adCampaignId, cancellationToken);
        if (campaign is null)
        {
            return NotRecorded("CampaignNotFound", "Campaign was not found.");
        }

        if (campaign.Status != AdCampaignStatus.Active)
        {
            return NotRecorded("CampaignNotActive", "Only active campaigns can record ad events.");
        }

        if (now < campaign.StartsAtUtc || now > campaign.EndsAtUtc)
        {
            return NotRecorded("CampaignNotInFlight", "Campaign is not currently in its scheduled flight window.");
        }

        var productInCampaign = await dbContext.AdCampaignProducts.AnyAsync(
            item => item.AdCampaignId == adCampaignId && item.ProductId == productId,
            cancellationToken);
        if (!productInCampaign)
        {
            return NotRecorded("ProductNotInCampaign", "Product is not attached to this campaign.");
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product?.Status != ProductStatus.Published)
        {
            return NotRecorded("ProductNotPublished", "Only published products can be promoted.");
        }

        var hasSellableStock = await dbContext.ProductVariants.AnyAsync(
            variant => variant.ProductId == productId
                && variant.Status == ProductVariantStatus.Active
                && variant.StockQuantity > variant.ReservedQuantity,
            cancellationToken);
        return hasSellableStock
            ? Recorded(null, "Trackable")
            : NotRecorded("ProductOutOfStock", "Out-of-stock products cannot record ad events.");
    }

    private async Task<decimal> GetAvailableClickChargeAsync(
        AdBudget budget,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var totalRemaining = budget.TotalBudget - budget.SpentAmount;
        if (totalRemaining <= 0)
        {
            return 0;
        }

        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var dailySpend = await dbContext.AdCharges
            .Where(charge => charge.AdCampaignId == budget.AdCampaignId
                && charge.ChargedAtUtc >= dayStart
                && charge.ChargedAtUtc < dayEnd)
            .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
        var dailyRemaining = budget.DailyBudget - dailySpend;
        if (dailyRemaining <= 0)
        {
            return 0;
        }

        return new[] { budget.MaxCostPerClick, totalRemaining, dailyRemaining }.Min();
    }

    private static AdTrackingResult Recorded(Guid? eventId, string status) =>
        new(true, eventId, status, null);

    private static AdTrackingResult NotRecorded(string status, string reason) =>
        new(false, null, status, reason);

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
