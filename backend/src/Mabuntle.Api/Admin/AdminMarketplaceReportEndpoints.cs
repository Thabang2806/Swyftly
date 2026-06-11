using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Refunds;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminMarketplaceReportEndpoints
{
    private const string DefaultCurrency = "ZAR";

    public static IEndpointRouteBuilder MapAdminMarketplaceReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reports")
            .WithTags("Admin Reports")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("/marketplace", GetMarketplaceReportAsync)
            .WithName("GetAdminMarketplaceReport")
            .WithSummary("Returns aggregate marketplace finance and operations reporting for admins.")
            .Produces<AdminMarketplaceReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/marketplace/export.csv", ExportMarketplaceReportCsvAsync)
            .WithName("ExportAdminMarketplaceReportCsv")
            .WithSummary("Exports the aggregate marketplace report as CSV.")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/buyer-growth", GetBuyerGrowthReportAsync)
            .WithName("GetAdminBuyerGrowthReport")
            .WithSummary("Returns aggregate buyer AI discovery telemetry for admins.")
            .Produces<AdminBuyerGrowthReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetMarketplaceReportAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        var report = await BuildReportAsync(range.FromUtc, range.ToUtc, dbContext, timeProvider, cancellationToken);
        return HttpResults.Ok(report);
    }

    private static async Task<IResult> ExportMarketplaceReportCsvAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        HttpResponse response,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        var report = await BuildReportAsync(range.FromUtc, range.ToUtc, dbContext, timeProvider, cancellationToken);
        response.Headers.ContentDisposition = "attachment; filename=\"mabuntle-marketplace-report.csv\"";

        return HttpResults.Text(BuildCsv(report), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> GetBuyerGrowthReportAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid || range.ToUtc.Subtract(range.FromUtc).TotalDays > 366)
        {
            return InvalidRangeProblem();
        }

        if (!TryResolveGrowthBucket(bucket, out var bucketKind))
        {
            return HttpResults.Problem(
                title: "Reports.InvalidBucket",
                detail: "bucket must be Day or Week.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var events = await dbContext.BuyerGrowthEvents
            .AsNoTracking()
            .Where(growthEvent => growthEvent.OccurredAtUtc >= range.FromUtc && growthEvent.OccurredAtUtc <= range.ToUtc)
            .ToListAsync(cancellationToken);
        var outcomes = await dbContext.BuyerGrowthOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.OccurredAtUtc >= range.FromUtc && outcome.OccurredAtUtc <= range.ToUtc)
            .ToListAsync(cancellationToken);

        var report = new AdminBuyerGrowthReportResponse(
            range.FromUtc,
            range.ToUtc,
            timeProvider.GetUtcNow(),
            bucketKind.ToString(),
            BuildBuyerGrowthSummary(events),
            BuildBuyerGrowthOutcomeSummary(outcomes),
            BuildBuyerGrowthBreakdown(
                events,
                growthEvent => growthEvent.ConfidenceBand?.ToString() ?? "Unknown",
                (name, count) => new AdminBuyerGrowthBreakdownResponse(name, count)),
            BuildBuyerGrowthBreakdown(
                events,
                growthEvent => growthEvent.SourceTool.ToString(),
                (name, count) => new AdminBuyerGrowthBreakdownResponse(name, count)),
            BuildBuyerGrowthOutcomeBreakdown(outcomes, outcome => outcome.SourceTool.ToString()),
            BuildBuyerGrowthOutcomeBreakdown(outcomes, outcome => outcome.ConfidenceBand?.ToString() ?? "Unknown"),
            BuildTopContext(events, growthEvent => growthEvent.Category),
            BuildTopContext(events, growthEvent => growthEvent.Colour),
            BuildTopContext(events, growthEvent => growthEvent.Material),
            BuildBuyerGrowthTrend(range.FromUtc, range.ToUtc, bucketKind, events, outcomes));

        return HttpResults.Ok(report);
    }

    private static async Task<AdminMarketplaceReportResponse> BuildReportAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.CreatedAtUtc >= fromUtc && order.CreatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var salesOrders = orders
            .Where(order => IsSalesOrderStatus(order.Status))
            .ToArray();

        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CreatedAtUtc >= fromUtc && entry.CreatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var refunded = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.Status == RefundStatus.Refunded
                && refund.RefundedAtUtc >= fromUtc
                && refund.RefundedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var sellerBalances = await dbContext.SellerBalances
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var terminalPayouts = await dbContext.SellerPayouts
            .AsNoTracking()
            .Where(payout => (payout.Status == SellerPayoutStatus.PaidOut || payout.Status == SellerPayoutStatus.Failed)
                && payout.UpdatedAtUtc >= fromUtc
                && payout.UpdatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var disputes = await dbContext.Disputes
            .AsNoTracking()
            .Where(dispute => dispute.OpenedAtUtc >= fromUtc && dispute.OpenedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var topSellers = await BuildTopSellersAsync(salesOrders, dbContext, cancellationToken);
        var topCategories = await BuildTopCategoriesAsync(salesOrders, dbContext, cancellationToken);

        var processedPayouts = terminalPayouts.Where(payout => payout.Status == SellerPayoutStatus.PaidOut).ToArray();
        var failedPayouts = terminalPayouts.Where(payout => payout.Status == SellerPayoutStatus.Failed).ToArray();
        var activeDisputes = disputes.Count(dispute => IsActiveDisputeStatus(dispute.Status));

        return new AdminMarketplaceReportResponse(
            fromUtc,
            toUtc,
            timeProvider.GetUtcNow(),
            DefaultCurrency,
            new AdminMarketplaceFinanceSummaryResponse(
                salesOrders.Sum(order => order.ItemsSubtotal),
                ledgerEntries
                    .Where(entry => entry.Type == LedgerEntryType.PlatformCommissionRecorded)
                    .Sum(entry => entry.Amount),
                ledgerEntries
                    .Where(entry => entry.Type == LedgerEntryType.PaymentProviderFeeRecorded)
                    .Sum(entry => entry.Amount),
                refunded.Sum(refund => refund.Amount),
                sellerBalances.Sum(balance => balance.PendingBalance),
                sellerBalances.Sum(balance => balance.AvailableBalance),
                sellerBalances.Sum(balance => balance.HeldBalance),
                processedPayouts.Sum(payout => payout.Amount),
                failedPayouts.Sum(payout => payout.Amount)),
            new AdminMarketplaceOperationsSummaryResponse(
                salesOrders.Length,
                refunded.Count,
                processedPayouts.Length,
                failedPayouts.Length,
                disputes.Count,
                activeDisputes),
            topSellers,
            topCategories,
            $"/api/admin/reports/marketplace/export.csv?fromUtc={Uri.EscapeDataString(fromUtc.ToString("O", CultureInfo.InvariantCulture))}&toUtc={Uri.EscapeDataString(toUtc.ToString("O", CultureInfo.InvariantCulture))}");
    }

    private static async Task<IReadOnlyCollection<AdminTopSellerReportRowResponse>> BuildTopSellersAsync(
        IReadOnlyCollection<Order> salesOrders,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sellerIds = salesOrders.Select(order => order.SellerId).Distinct().ToArray();
        var sellers = await dbContext.SellerProfiles
            .AsNoTracking()
            .Where(seller => sellerIds.Contains(seller.Id))
            .Select(seller => new { seller.Id, seller.DisplayName })
            .ToDictionaryAsync(seller => seller.Id, seller => seller.DisplayName, cancellationToken);

        return salesOrders
            .GroupBy(order => order.SellerId)
            .Select(group => new AdminTopSellerReportRowResponse(
                group.Key,
                sellers.GetValueOrDefault(group.Key),
                group.Count(),
                group.Sum(order => order.ItemsSubtotal),
                group.SelectMany(order => order.Items).Sum(item => item.Quantity)))
            .OrderByDescending(seller => seller.GrossMerchandiseValue)
            .ThenByDescending(seller => seller.OrderCount)
            .Take(10)
            .ToArray();
    }

    private static async Task<IReadOnlyCollection<AdminTopCategoryReportRowResponse>> BuildTopCategoriesAsync(
        IReadOnlyCollection<Order> salesOrders,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var salesItems = salesOrders.SelectMany(order => order.Items).ToArray();
        var productIds = salesItems.Select(item => item.ProductId).Distinct().ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.CategoryId })
            .ToDictionaryAsync(product => product.Id, product => product.CategoryId, cancellationToken);
        var categoryIds = products.Values.Where(categoryId => categoryId.HasValue).Select(categoryId => categoryId!.Value).Distinct().ToArray();
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        return salesItems
            .GroupBy(item => products.GetValueOrDefault(item.ProductId))
            .Select(group =>
            {
                var categoryId = group.Key;
                return new AdminTopCategoryReportRowResponse(
                    categoryId,
                    categoryId.HasValue ? categories.GetValueOrDefault(categoryId.Value) : "Uncategorised",
                    group.Sum(item => item.Quantity),
                    group.Sum(item => item.LineTotal));
            })
            .OrderByDescending(category => category.Revenue)
            .ThenByDescending(category => category.QuantitySold)
            .Take(10)
            .ToArray();
    }

    private static ReportDateRange ResolveRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, TimeProvider timeProvider)
    {
        var resolvedTo = (toUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var resolvedFrom = (fromUtc ?? resolvedTo.AddDays(-30)).ToUniversalTime();

        return new ReportDateRange(resolvedFrom, resolvedTo, resolvedFrom <= resolvedTo);
    }

    private static bool TryResolveGrowthBucket(string? bucket, out BuyerGrowthReportBucket bucketKind)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            bucketKind = BuyerGrowthReportBucket.Day;
            return true;
        }

        return Enum.TryParse(bucket, ignoreCase: true, out bucketKind)
            && Enum.IsDefined(bucketKind);
    }

    private static AdminBuyerGrowthSummaryResponse BuildBuyerGrowthSummary(IReadOnlyCollection<BuyerGrowthEvent> events) =>
        new(
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.AssistantSearchSubmitted)
                + CountBuyerGrowthEvents(events, BuyerGrowthEventType.VisualSearchSubmitted),
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.AssistantShopHandoff)
                + CountBuyerGrowthEvents(events, BuyerGrowthEventType.VisualShopHandoff),
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.AssistantProductOpened)
                + CountBuyerGrowthEvents(events, BuyerGrowthEventType.VisualProductOpened),
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.AssistantFeedbackSubmitted)
                + CountBuyerGrowthEvents(events, BuyerGrowthEventType.VisualFeedbackSubmitted),
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.AssistantSearchSubmitted),
            CountBuyerGrowthEvents(events, BuyerGrowthEventType.VisualSearchSubmitted));

    private static AdminBuyerGrowthOutcomeSummaryResponse BuildBuyerGrowthOutcomeSummary(IReadOnlyCollection<BuyerGrowthOutcome> outcomes)
    {
        var productOpenedCount = CountBuyerGrowthOutcomes(outcomes, BuyerGrowthOutcomeType.ProductOpened);
        var addToCartCount = CountBuyerGrowthOutcomes(outcomes, BuyerGrowthOutcomeType.ProductAddedToCart);
        var checkoutStartedCount = CountDistinctCarts(outcomes, BuyerGrowthOutcomeType.CheckoutStarted);
        var orderCreatedCount = CountDistinctOrders(outcomes, BuyerGrowthOutcomeType.OrderCreated);
        var paidOrderCount = CountDistinctOrders(outcomes, BuyerGrowthOutcomeType.OrderPaid);

        return new AdminBuyerGrowthOutcomeSummaryResponse(
            productOpenedCount,
            addToCartCount,
            checkoutStartedCount,
            orderCreatedCount,
            paidOrderCount,
            SafeRate(addToCartCount, productOpenedCount),
            SafeRate(checkoutStartedCount, addToCartCount),
            SafeRate(orderCreatedCount, checkoutStartedCount),
            SafeRate(paidOrderCount, orderCreatedCount));
    }

    private static IReadOnlyCollection<TBreakdown> BuildBuyerGrowthBreakdown<TBreakdown>(
        IReadOnlyCollection<BuyerGrowthEvent> events,
        Func<BuyerGrowthEvent, string> keySelector,
        Func<string, int, TBreakdown> factory) =>
        events
            .GroupBy(keySelector)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .Select(item => factory(item.Name, item.Count))
            .ToArray();

    private static IReadOnlyCollection<AdminBuyerGrowthContextResponse> BuildTopContext(
        IReadOnlyCollection<BuyerGrowthEvent> events,
        Func<BuyerGrowthEvent, string?> selector) =>
        events
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminBuyerGrowthContextResponse(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Value)
            .Take(10)
            .ToArray();

    private static IReadOnlyCollection<AdminBuyerGrowthOutcomeBreakdownResponse> BuildBuyerGrowthOutcomeBreakdown(
        IReadOnlyCollection<BuyerGrowthOutcome> outcomes,
        Func<BuyerGrowthOutcome, string> keySelector) =>
        outcomes
            .GroupBy(keySelector)
            .Select(group => new AdminBuyerGrowthOutcomeBreakdownResponse(
                group.Key,
                CountBuyerGrowthOutcomes(group, BuyerGrowthOutcomeType.ProductOpened),
                CountBuyerGrowthOutcomes(group, BuyerGrowthOutcomeType.ProductAddedToCart),
                CountDistinctCarts(group, BuyerGrowthOutcomeType.CheckoutStarted),
                CountDistinctOrders(group, BuyerGrowthOutcomeType.OrderCreated),
                CountDistinctOrders(group, BuyerGrowthOutcomeType.OrderPaid)))
            .OrderByDescending(item => item.ProductOpenedCount + item.AddToCartCount + item.CheckoutStartedCount + item.OrderCreatedCount + item.PaidOrderCount)
            .ThenBy(item => item.Name)
            .ToArray();

    private static IReadOnlyCollection<AdminBuyerGrowthTrendBucketResponse> BuildBuyerGrowthTrend(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        BuyerGrowthReportBucket bucketKind,
        IReadOnlyCollection<BuyerGrowthEvent> events,
        IReadOnlyCollection<BuyerGrowthOutcome> outcomes)
    {
        var buckets = new List<AdminBuyerGrowthTrendBucketResponse>();
        var cursor = fromUtc;
        var step = bucketKind == BuyerGrowthReportBucket.Week ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);

        while (cursor <= toUtc)
        {
            var bucketEnd = cursor.Add(step);
            var bucketEvents = events
                .Where(growthEvent => growthEvent.OccurredAtUtc >= cursor && growthEvent.OccurredAtUtc < bucketEnd)
                .ToArray();
            var bucketOutcomes = outcomes
                .Where(outcome => outcome.OccurredAtUtc >= cursor && outcome.OccurredAtUtc < bucketEnd)
                .ToArray();
            var summary = BuildBuyerGrowthSummary(bucketEvents);
            buckets.Add(new AdminBuyerGrowthTrendBucketResponse(
                cursor,
                bucketEnd > toUtc ? toUtc : bucketEnd,
                summary.SearchSubmittedCount,
                summary.ShopHandoffCount,
                summary.ProductOpenedCount,
                summary.FeedbackSubmittedCount,
                CountBuyerGrowthOutcomes(bucketOutcomes, BuyerGrowthOutcomeType.ProductOpened),
                CountBuyerGrowthOutcomes(bucketOutcomes, BuyerGrowthOutcomeType.ProductAddedToCart),
                CountDistinctCarts(bucketOutcomes, BuyerGrowthOutcomeType.CheckoutStarted),
                CountDistinctOrders(bucketOutcomes, BuyerGrowthOutcomeType.OrderCreated),
                CountDistinctOrders(bucketOutcomes, BuyerGrowthOutcomeType.OrderPaid)));
            cursor = bucketEnd;
        }

        return buckets;
    }

    private static int CountBuyerGrowthEvents(IReadOnlyCollection<BuyerGrowthEvent> events, BuyerGrowthEventType eventType) =>
        events.Count(growthEvent => growthEvent.EventType == eventType);

    private static int CountBuyerGrowthOutcomes(IEnumerable<BuyerGrowthOutcome> outcomes, BuyerGrowthOutcomeType outcomeType) =>
        outcomes.Count(outcome => outcome.OutcomeType == outcomeType);

    private static int CountDistinctCarts(IEnumerable<BuyerGrowthOutcome> outcomes, BuyerGrowthOutcomeType outcomeType) =>
        outcomes
            .Where(outcome => outcome.OutcomeType == outcomeType)
            .Select(outcome => outcome.CartId)
            .Where(cartId => cartId.HasValue)
            .Distinct()
            .Count();

    private static int CountDistinctOrders(IEnumerable<BuyerGrowthOutcome> outcomes, BuyerGrowthOutcomeType outcomeType) =>
        outcomes
            .Where(outcome => outcome.OutcomeType == outcomeType)
            .Select(outcome => outcome.OrderId)
            .Where(orderId => orderId.HasValue)
            .Distinct()
            .Count();

    private static decimal SafeRate(int numerator, int denominator) =>
        denominator <= 0 ? 0 : decimal.Round((decimal)numerator / denominator, 4);

    private static bool IsSalesOrderStatus(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.Processing
            or OrderStatus.ReadyToShip
            or OrderStatus.Shipped
            or OrderStatus.Delivered
            or OrderStatus.ReturnRequested
            or OrderStatus.Disputed
            or OrderStatus.Completed;

    private static bool IsActiveDisputeStatus(DisputeStatus status) =>
        status is DisputeStatus.Open
            or DisputeStatus.AwaitingBuyer
            or DisputeStatus.AwaitingSeller
            or DisputeStatus.UnderAdminReview;

    private static IResult InvalidRangeProblem() =>
        HttpResults.Problem(
            title: "Reports.InvalidDateRange",
            detail: "fromUtc must be earlier than or equal to toUtc.",
            statusCode: StatusCodes.Status400BadRequest);

    private static string BuildCsv(AdminMarketplaceReportResponse report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("section,metric,value,currency");
        AppendCsvLine(builder, "range", "fromUtc", report.FromUtc.ToString("O", CultureInfo.InvariantCulture), string.Empty);
        AppendCsvLine(builder, "range", "toUtc", report.ToUtc.ToString("O", CultureInfo.InvariantCulture), string.Empty);
        AppendCsvLine(builder, "finance", "grossMerchandiseValue", report.Finance.GrossMerchandiseValue, report.Currency);
        AppendCsvLine(builder, "finance", "platformCommissionEarned", report.Finance.PlatformCommissionEarned, report.Currency);
        AppendCsvLine(builder, "finance", "paymentProcessingFees", report.Finance.PaymentProcessingFees, report.Currency);
        AppendCsvLine(builder, "finance", "refunds", report.Finance.Refunds, report.Currency);
        AppendCsvLine(builder, "finance", "sellerPendingBalances", report.Finance.SellerPendingBalances, report.Currency);
        AppendCsvLine(builder, "finance", "sellerAvailableBalances", report.Finance.SellerAvailableBalances, report.Currency);
        AppendCsvLine(builder, "finance", "sellerHeldBalances", report.Finance.SellerHeldBalances, report.Currency);
        AppendCsvLine(builder, "finance", "payoutsProcessed", report.Finance.PayoutsProcessed, report.Currency);
        AppendCsvLine(builder, "finance", "failedPayouts", report.Finance.FailedPayouts, report.Currency);
        AppendCsvLine(builder, "operations", "orderCount", report.Operations.OrderCount, string.Empty);
        AppendCsvLine(builder, "operations", "refundCount", report.Operations.RefundCount, string.Empty);
        AppendCsvLine(builder, "operations", "payoutsProcessedCount", report.Operations.PayoutsProcessedCount, string.Empty);
        AppendCsvLine(builder, "operations", "failedPayoutCount", report.Operations.FailedPayoutCount, string.Empty);
        AppendCsvLine(builder, "operations", "disputeCount", report.Operations.DisputeCount, string.Empty);
        AppendCsvLine(builder, "operations", "activeDisputeCount", report.Operations.ActiveDisputeCount, string.Empty);
        return builder.ToString();
    }

    private static void AppendCsvLine(StringBuilder builder, string section, string metric, object value, string currency)
    {
        builder
            .Append(Csv(section))
            .Append(',')
            .Append(Csv(metric))
            .Append(',')
            .Append(Csv(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty))
            .Append(',')
            .Append(Csv(currency))
            .AppendLine();
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private sealed record ReportDateRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc, bool IsValid);

    private enum BuyerGrowthReportBucket
    {
        Day,
        Week
    }
}

public sealed record AdminMarketplaceReportResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset GeneratedAtUtc,
    string Currency,
    AdminMarketplaceFinanceSummaryResponse Finance,
    AdminMarketplaceOperationsSummaryResponse Operations,
    IReadOnlyCollection<AdminTopSellerReportRowResponse> TopSellers,
    IReadOnlyCollection<AdminTopCategoryReportRowResponse> TopCategories,
    string CsvExportUrl);

public sealed record AdminMarketplaceFinanceSummaryResponse(
    decimal GrossMerchandiseValue,
    decimal PlatformCommissionEarned,
    decimal PaymentProcessingFees,
    decimal Refunds,
    decimal SellerPendingBalances,
    decimal SellerAvailableBalances,
    decimal SellerHeldBalances,
    decimal PayoutsProcessed,
    decimal FailedPayouts);

public sealed record AdminMarketplaceOperationsSummaryResponse(
    int OrderCount,
    int RefundCount,
    int PayoutsProcessedCount,
    int FailedPayoutCount,
    int DisputeCount,
    int ActiveDisputeCount);

public sealed record AdminTopSellerReportRowResponse(
    Guid SellerId,
    string? SellerDisplayName,
    int OrderCount,
    decimal GrossMerchandiseValue,
    int ItemsSold);

public sealed record AdminTopCategoryReportRowResponse(
    Guid? CategoryId,
    string? CategoryName,
    int QuantitySold,
    decimal Revenue);

public sealed record AdminBuyerGrowthReportResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset GeneratedAtUtc,
    string Bucket,
    AdminBuyerGrowthSummaryResponse Summary,
    AdminBuyerGrowthOutcomeSummaryResponse OutcomeSummary,
    IReadOnlyCollection<AdminBuyerGrowthBreakdownResponse> ConfidenceBreakdown,
    IReadOnlyCollection<AdminBuyerGrowthBreakdownResponse> SourceToolBreakdown,
    IReadOnlyCollection<AdminBuyerGrowthOutcomeBreakdownResponse> OutcomeSourceToolBreakdown,
    IReadOnlyCollection<AdminBuyerGrowthOutcomeBreakdownResponse> OutcomeConfidenceBreakdown,
    IReadOnlyCollection<AdminBuyerGrowthContextResponse> TopCategories,
    IReadOnlyCollection<AdminBuyerGrowthContextResponse> TopColours,
    IReadOnlyCollection<AdminBuyerGrowthContextResponse> TopMaterials,
    IReadOnlyCollection<AdminBuyerGrowthTrendBucketResponse> Trend);

public sealed record AdminBuyerGrowthSummaryResponse(
    int SearchSubmittedCount,
    int ShopHandoffCount,
    int ProductOpenedCount,
    int FeedbackSubmittedCount,
    int AssistantSearchCount,
    int VisualSearchCount);

public sealed record AdminBuyerGrowthOutcomeSummaryResponse(
    int ProductOpenedCount,
    int AddToCartCount,
    int CheckoutStartedCount,
    int OrderCreatedCount,
    int PaidOrderCount,
    decimal ProductOpenToCartRate,
    decimal CartToCheckoutRate,
    decimal CheckoutToOrderRate,
    decimal OrderToPaidRate);

public sealed record AdminBuyerGrowthBreakdownResponse(string Name, int Count);

public sealed record AdminBuyerGrowthOutcomeBreakdownResponse(
    string Name,
    int ProductOpenedCount,
    int AddToCartCount,
    int CheckoutStartedCount,
    int OrderCreatedCount,
    int PaidOrderCount);

public sealed record AdminBuyerGrowthContextResponse(string Value, int Count);

public sealed record AdminBuyerGrowthTrendBucketResponse(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int SearchSubmittedCount,
    int ShopHandoffCount,
    int ProductOpenedCount,
    int FeedbackSubmittedCount,
    int AttributedProductOpenCount,
    int AddToCartCount,
    int CheckoutStartedCount,
    int OrderCreatedCount,
    int PaidOrderCount);
