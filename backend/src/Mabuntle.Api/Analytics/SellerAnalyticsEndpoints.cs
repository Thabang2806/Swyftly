using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Ai;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Domain.Support;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Analytics;

public static class SellerAnalyticsEndpoints
{
    private const string DefaultCurrency = "ZAR";
    private const int MaxRangeDays = 366;
    private static readonly string[] FunnelSourceCategories =
    [
        "Direct",
        "Search",
        "Social",
        "Email",
        "Ads",
        "Referral",
        "Unknown"
    ];

    public static IEndpointRouteBuilder MapSellerAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/seller/analytics/summary", GetSummaryAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsSummary")
            .WithSummary("Returns aggregate seller-owned sales, product, ad, and AI usage metrics.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerAnalyticsSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/seller/analytics/performance", GetPerformanceAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsPerformance")
            .WithSummary("Returns seller-owned trend, product, inventory, ad, and customer-care analytics for a date range.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerAnalyticsPerformanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/seller/analytics/export.csv", ExportCsvAsync)
            .WithTags("Seller Analytics")
            .WithName("ExportSellerAnalyticsCsv")
            .WithSummary("Exports seller-owned analytics as CSV.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/seller/analytics/report-schedule", GetReportScheduleAsync)
            .WithTags("Seller Analytics")
            .WithName("GetSellerAnalyticsReportSchedule")
            .WithSummary("Returns the authenticated seller's scheduled analytics report configuration.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerReportScheduleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/seller/analytics/report-schedule", UpdateReportScheduleAsync)
            .WithTags("Seller Analytics")
            .WithName("UpdateSellerAnalyticsReportSchedule")
            .WithSummary("Updates the authenticated seller's scheduled analytics report configuration.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerReportScheduleResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/seller/analytics/report-schedule/send-test", SendTestReportDigestAsync)
            .WithTags("Seller Analytics")
            .WithName("SendSellerAnalyticsTestDigest")
            .WithSummary("Queues a test seller analytics digest notification and email according to preferences.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerReportDigestSendResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == seller.Id)
            .ToListAsync(cancellationToken);
        var salesOrders = orders
            .Where(order => IsSalesOrderStatus(order.Status))
            .ToArray();
        var totalSales = salesOrders.Sum(order => order.TotalAmount);
        var orderCount = salesOrders.Length;
        var productsSold = salesOrders.SelectMany(order => order.Items).Sum(item => item.Quantity);
        var totalRefunded = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == seller.Id && refund.Status == RefundStatus.Refunded)
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0m;
        var returnCount = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(returnRequest => returnRequest.SellerId == seller.Id, cancellationToken);
        var refundedOrderCount = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == seller.Id && refund.Status == RefundStatus.Refunded)
            .Select(refund => refund.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);

        var topProducts = salesOrders
            .SelectMany(order => order.Items)
            .GroupBy(item => new { item.ProductId, item.ProductTitle })
            .Select(group => new SellerTopProductResponse(
                group.Key.ProductId,
                group.Key.ProductTitle,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.LineTotal)))
            .OrderByDescending(product => product.QuantitySold)
            .ThenByDescending(product => product.Revenue)
            .Take(5)
            .ToArray();
        var lowStockProducts = await GetLowStockProductsAsync(seller.Id, dbContext, cancellationToken);
        var adPerformance = await GetAdPerformanceAsync(seller.Id, dbContext, cancellationToken);
        var aiUsage = await GetAiUsageAsync(seller.Id, dbContext, cancellationToken);
        var funnelEvents = await dbContext.SellerFunnelEvents
            .AsNoTracking()
            .Where(funnelEvent => funnelEvent.SellerId == seller.Id)
            .ToListAsync(cancellationToken);
        var funnelSummary = BuildFunnelSummary(funnelEvents);

        return HttpResults.Ok(new SellerAnalyticsSummaryResponse(
            seller.Id,
            totalSales,
            orderCount,
            orderCount == 0 ? 0 : decimal.Round(totalSales / orderCount, 2),
            funnelSummary.ProductViews == 0 ? 0 : decimal.Round((decimal)funnelSummary.PaidOrderCount / funnelSummary.ProductViews, 4),
            productsSold,
            totalRefunded,
            orderCount == 0 ? 0 : decimal.Round((decimal)refundedOrderCount / orderCount, 4),
            orderCount == 0 ? 0 : decimal.Round((decimal)returnCount / orderCount, 4),
            topProducts,
            lowStockProducts,
            adPerformance,
            aiUsage));
    }

    private static async Task<IResult> GetPerformanceAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? sourceCategory,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        if (!TryResolveBucket(bucket, out var bucketKind))
        {
            return InvalidBucketProblem();
        }

        if (!TryResolveSourceCategory(sourceCategory, out var normalizedSourceCategory))
        {
            return InvalidSourceCategoryProblem();
        }

        var report = await BuildPerformanceAsync(
            seller.Id,
            range.FromUtc,
            range.ToUtc,
            bucketKind,
            normalizedSourceCategory,
            dbContext,
            cancellationToken);
        return HttpResults.Ok(report);
    }

    private static async Task<IResult> ExportCsvAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? report,
        string? sourceCategory,
        ClaimsPrincipal principal,
        HttpResponse response,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        if (!TryResolveBucket(bucket, out var bucketKind))
        {
            return InvalidBucketProblem();
        }

        if (!TryResolveReport(report, out var reportKind))
        {
            return InvalidReportProblem();
        }

        if (!TryResolveSourceCategory(sourceCategory, out var normalizedSourceCategory))
        {
            return InvalidSourceCategoryProblem();
        }

        var analytics = await BuildPerformanceAsync(
            seller.Id,
            range.FromUtc,
            range.ToUtc,
            bucketKind,
            normalizedSourceCategory,
            dbContext,
            cancellationToken);
        response.Headers.ContentDisposition = $"attachment; filename=\"mabuntle-seller-analytics-{reportKind.ToString().ToLowerInvariant()}.csv\"";

        return HttpResults.Text(BuildCsv(analytics, reportKind), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> GetReportScheduleAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        ISellerScheduledReportService reportService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        return HttpResults.Ok(await reportService.GetOrCreateScheduleAsync(
            seller.Id,
            timeProvider.GetUtcNow(),
            cancellationToken));
    }

    private static async Task<IResult> UpdateReportScheduleAsync(
        SellerReportScheduleRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        ISellerScheduledReportService reportService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var result = await reportService.SaveScheduleAsync(
            seller.Id,
            seller.VerificationStatus == SellerVerificationStatus.Verified,
            request,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (result.IsSuccess)
        {
            return HttpResults.Ok(result.Schedule);
        }

        if (result.ConflictTitle is not null)
        {
            return HttpResults.Problem(
                title: result.ConflictTitle,
                detail: result.ConflictDetail,
                statusCode: StatusCodes.Status409Conflict);
        }

        return HttpResults.ValidationProblem(result.Errors);
    }

    private static async Task<IResult> SendTestReportDigestAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        ISellerScheduledReportService reportService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (seller.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return HttpResults.Problem(
                title: "SellerAnalytics.ScheduleRequiresVerification",
                detail: "Test analytics digests can be sent only after seller verification is approved.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var result = await reportService.SendTestDigestAsync(
            seller.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return result.IsSuccess
            ? HttpResults.Ok(result)
            : HttpResults.Problem(
                title: "SellerAnalytics.TestDigestFailed",
                detail: result.FailureReason ?? "Test digest could not be sent.",
                statusCode: StatusCodes.Status500InternalServerError);
    }

    private static async Task<SellerAnalyticsPerformanceResponse> BuildPerformanceAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AnalyticsBucketKind bucket,
        string? sourceCategory,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == sellerId
                && order.CreatedAtUtc >= fromUtc
                && order.CreatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var salesOrders = orders
            .Where(order => IsSalesOrderStatus(order.Status))
            .ToArray();

        var refunds = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == sellerId
                && refund.Status == RefundStatus.Refunded
                && refund.RefundedAtUtc >= fromUtc
                && refund.RefundedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var returns = await dbContext.ReturnRequests
            .AsNoTracking()
            .Include(returnRequest => returnRequest.Items)
            .Where(returnRequest => returnRequest.SellerId == sellerId
                && returnRequest.RequestedAtUtc >= fromUtc
                && returnRequest.RequestedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .Select(product => new ProductAnalyticsProjection(
                product.Id,
                product.Title,
                product.Slug,
                product.Status))
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.ProductId).ToArray();
        var variants = productIds.Length == 0
            ? []
            : await dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId))
                .Select(variant => new VariantAnalyticsProjection(
                    variant.Id,
                    variant.ProductId,
                    variant.Sku,
                    variant.Barcode,
                    variant.Size,
                    variant.Colour,
                    variant.Status,
                    variant.StockQuantity,
                    variant.ReservedQuantity,
                    variant.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        var movements = productIds.Length == 0
            ? []
            : await dbContext.InventoryMovements
                .AsNoTracking()
                .Where(movement => movement.SellerId == sellerId)
                .GroupBy(movement => movement.ProductVariantId)
                .Select(group => new InventoryMovementLastActivityProjection(
                    group.Key,
                    group.Max(movement => movement.OccurredAtUtc)))
                .ToListAsync(cancellationToken);
        var allFunnelEvents = await dbContext.SellerFunnelEvents
            .AsNoTracking()
            .Where(funnelEvent => funnelEvent.SellerId == sellerId
                && funnelEvent.OccurredAtUtc >= fromUtc
                && funnelEvent.OccurredAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var funnelEvents = string.IsNullOrWhiteSpace(sourceCategory)
            ? allFunnelEvents
            : allFunnelEvents
                .Where(funnelEvent => string.Equals(
                    NormalizeFunnelSourceCategory(funnelEvent.SourceCategory),
                    sourceCategory,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        var salesTrend = BuildSalesTrend(fromUtc, toUtc, bucket, salesOrders, refunds);
        var productPerformance = BuildProductPerformance(products, variants, salesOrders, refunds, returns);
        var inventoryPerformance = BuildInventoryPerformance(products, variants, movements);
        var adPerformance = await BuildAdPerformanceAsync(sellerId, fromUtc, toUtc, dbContext, cancellationToken);
        var customerCareSummary = await BuildCustomerCareSummaryAsync(
            sellerId,
            fromUtc,
            toUtc,
            refunds,
            returns,
            dbContext,
            cancellationToken);
        var funnelSummary = BuildFunnelSummary(funnelEvents);
        var funnelTrend = BuildFunnelTrend(fromUtc, toUtc, bucket, funnelEvents);
        var productFunnel = BuildProductFunnel(products, funnelEvents, salesOrders);
        var sourceBreakdown = BuildFunnelSourceBreakdown(allFunnelEvents);

        return new SellerAnalyticsPerformanceResponse(
            sellerId,
            fromUtc,
            toUtc,
            bucket.ToString(),
            salesTrend,
            productPerformance,
            inventoryPerformance,
            adPerformance,
            customerCareSummary,
            funnelSummary,
            funnelTrend,
            productFunnel,
            sourceBreakdown);
    }

    private static IReadOnlyCollection<SellerSalesTrendBucketResponse> BuildSalesTrend(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AnalyticsBucketKind bucket,
        IReadOnlyCollection<Order> salesOrders,
        IReadOnlyCollection<Refund> refunds)
    {
        var buckets = BuildBuckets(fromUtc, toUtc, bucket);

        return buckets
            .Select(currentBucket =>
            {
                var bucketOrders = salesOrders
                    .Where(order => IsInBucket(order.CreatedAtUtc, currentBucket.PeriodStartUtc, currentBucket.PeriodEndUtc, toUtc))
                    .ToArray();
                var bucketRefunds = refunds
                    .Where(refund => refund.RefundedAtUtc.HasValue
                        && IsInBucket(refund.RefundedAtUtc.Value, currentBucket.PeriodStartUtc, currentBucket.PeriodEndUtc, toUtc))
                    .ToArray();
                var grossSales = bucketOrders.Sum(order => order.TotalAmount);
                var refundedAmount = bucketRefunds.Sum(refund => refund.Amount);

                return new SellerSalesTrendBucketResponse(
                    currentBucket.PeriodStartUtc,
                    currentBucket.PeriodEndUtc,
                    bucketOrders.Length,
                    grossSales,
                    refundedAmount,
                    grossSales - refundedAmount,
                    bucketOrders.SelectMany(order => order.Items).Sum(item => item.Quantity));
            })
            .ToArray();
    }

    private static SellerFunnelSummaryResponse BuildFunnelSummary(IReadOnlyCollection<SellerFunnelEvent> funnelEvents)
    {
        var storefrontViews = CountFunnelEvents(funnelEvents, SellerFunnelEventType.StorefrontViewed);
        var productViews = CountFunnelEvents(funnelEvents, SellerFunnelEventType.ProductViewed);
        var addToCartCount = CountFunnelEvents(funnelEvents, SellerFunnelEventType.ProductAddedToCart);
        var checkoutStartCount = CountFunnelEvents(funnelEvents, SellerFunnelEventType.CheckoutStarted);
        var orderCreatedCount = CountFunnelEvents(funnelEvents, SellerFunnelEventType.OrderCreated);
        var paidOrderCount = CountFunnelEvents(funnelEvents, SellerFunnelEventType.OrderPaid);

        return new SellerFunnelSummaryResponse(
            storefrontViews,
            productViews,
            addToCartCount,
            checkoutStartCount,
            orderCreatedCount,
            paidOrderCount,
            Rate(addToCartCount, productViews),
            Rate(paidOrderCount, checkoutStartCount));
    }

    private static IReadOnlyCollection<SellerFunnelTrendBucketResponse> BuildFunnelTrend(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AnalyticsBucketKind bucket,
        IReadOnlyCollection<SellerFunnelEvent> funnelEvents)
    {
        return BuildBuckets(fromUtc, toUtc, bucket)
            .Select(currentBucket =>
            {
                var bucketEvents = funnelEvents
                    .Where(funnelEvent => IsInBucket(funnelEvent.OccurredAtUtc, currentBucket.PeriodStartUtc, currentBucket.PeriodEndUtc, toUtc))
                    .ToArray();
                var productViews = CountFunnelEvents(bucketEvents, SellerFunnelEventType.ProductViewed);
                var addToCartCount = CountFunnelEvents(bucketEvents, SellerFunnelEventType.ProductAddedToCart);
                var checkoutStartCount = CountFunnelEvents(bucketEvents, SellerFunnelEventType.CheckoutStarted);
                var paidOrderCount = CountFunnelEvents(bucketEvents, SellerFunnelEventType.OrderPaid);

                return new SellerFunnelTrendBucketResponse(
                    currentBucket.PeriodStartUtc,
                    currentBucket.PeriodEndUtc,
                    CountFunnelEvents(bucketEvents, SellerFunnelEventType.StorefrontViewed),
                    productViews,
                    addToCartCount,
                    checkoutStartCount,
                    CountFunnelEvents(bucketEvents, SellerFunnelEventType.OrderCreated),
                    paidOrderCount,
                    Rate(addToCartCount, productViews),
                    Rate(paidOrderCount, checkoutStartCount));
            })
            .ToArray();
    }

    private static IReadOnlyCollection<SellerProductFunnelResponse> BuildProductFunnel(
        IReadOnlyCollection<ProductAnalyticsProjection> products,
        IReadOnlyCollection<SellerFunnelEvent> funnelEvents,
        IReadOnlyCollection<Order> salesOrders)
    {
        return products
            .Select(product =>
            {
                var productEvents = funnelEvents
                    .Where(funnelEvent => funnelEvent.ProductId == product.ProductId)
                    .ToArray();
                var productViews = CountFunnelEvents(productEvents, SellerFunnelEventType.ProductViewed);
                var addToCartCount = CountFunnelEvents(productEvents, SellerFunnelEventType.ProductAddedToCart);
                var paidOrderItems = salesOrders
                    .SelectMany(order => order.Items.Select(item => new { Order = order, Item = item }))
                    .Where(pair => pair.Item.ProductId == product.ProductId)
                    .ToArray();
                var paidOrderCount = paidOrderItems
                    .Select(pair => pair.Order.Id)
                    .Distinct()
                    .Count();
                var revenue = paidOrderItems.Sum(pair => pair.Item.LineTotal);
                var dominantSourceCategory = productEvents
                    .GroupBy(funnelEvent => NormalizeFunnelSourceCategory(funnelEvent.SourceCategory))
                    .OrderByDescending(group => group.Count(item => item.EventType == SellerFunnelEventType.ProductViewed))
                    .ThenByDescending(group => group.Count())
                    .Select(group => group.Key)
                    .FirstOrDefault() ?? "Unknown";
                var topUtmSource = MostCommon(productEvents.Select(funnelEvent => funnelEvent.UtmSource));
                var topReferrerHost = MostCommon(productEvents.Select(funnelEvent => funnelEvent.ReferrerHost));

                return new SellerProductFunnelResponse(
                    product.ProductId,
                    product.Title,
                    product.Slug,
                    productViews,
                    addToCartCount,
                    paidOrderCount,
                    revenue,
                    Rate(addToCartCount, productViews),
                    Rate(paidOrderCount, productViews),
                    dominantSourceCategory,
                    topUtmSource,
                    topReferrerHost);
            })
            .Where(row => row.ProductViews > 0 || row.AddToCartCount > 0 || row.PaidOrderCount > 0)
            .OrderByDescending(row => row.ProductViews)
            .ThenByDescending(row => row.AddToCartCount)
            .ThenByDescending(row => row.Revenue)
            .ToArray();
    }

    private static IReadOnlyCollection<SellerFunnelSourceBreakdownResponse> BuildFunnelSourceBreakdown(
        IReadOnlyCollection<SellerFunnelEvent> funnelEvents)
    {
        return funnelEvents
            .GroupBy(funnelEvent => NormalizeFunnelSourceCategory(funnelEvent.SourceCategory))
            .Select(group =>
            {
                var events = group.ToArray();
                var productViews = CountFunnelEvents(events, SellerFunnelEventType.ProductViewed);
                var addToCartCount = CountFunnelEvents(events, SellerFunnelEventType.ProductAddedToCart);
                var checkoutStartCount = CountFunnelEvents(events, SellerFunnelEventType.CheckoutStarted);
                var paidOrderCount = CountFunnelEvents(events, SellerFunnelEventType.OrderPaid);

                return new SellerFunnelSourceBreakdownResponse(
                    group.Key,
                    CountFunnelEvents(events, SellerFunnelEventType.StorefrontViewed),
                    productViews,
                    addToCartCount,
                    checkoutStartCount,
                    CountFunnelEvents(events, SellerFunnelEventType.OrderCreated),
                    paidOrderCount,
                    Rate(addToCartCount, productViews),
                    Rate(paidOrderCount, checkoutStartCount),
                    MostCommon(events.Select(item => item.UtmSource)),
                    MostCommon(events.Select(item => item.ReferrerHost)));
            })
            .OrderByDescending(source => source.ProductViews)
            .ThenByDescending(source => source.AddToCartCount)
            .ThenBy(source => source.SourceCategory)
            .ToArray();
    }

    private static int CountFunnelEvents(IReadOnlyCollection<SellerFunnelEvent> events, SellerFunnelEventType eventType) =>
        events.Count(funnelEvent => funnelEvent.EventType == eventType);

    private static decimal Rate(int numerator, int denominator) =>
        denominator == 0 ? 0 : decimal.Round((decimal)numerator / denominator, 4);

    private static string NormalizeFunnelSourceCategory(string? sourceCategory)
    {
        var match = FunnelSourceCategories.FirstOrDefault(category =>
            string.Equals(category, sourceCategory?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? "Unknown";
    }

    private static string? MostCommon(IEnumerable<string?> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

    private static IReadOnlyCollection<SellerProductPerformanceResponse> BuildProductPerformance(
        IReadOnlyCollection<ProductAnalyticsProjection> products,
        IReadOnlyCollection<VariantAnalyticsProjection> variants,
        IReadOnlyCollection<Order> salesOrders,
        IReadOnlyCollection<Refund> refunds,
        IReadOnlyCollection<ReturnRequest> returns)
    {
        var orderRefunds = refunds
            .GroupBy(refund => refund.OrderId)
            .ToDictionary(group => group.Key, group => group.Sum(refund => refund.Amount));

        return products
            .Select(product =>
            {
                var orderItems = salesOrders
                    .SelectMany(order => order.Items.Select(item => new { Order = order, Item = item }))
                    .Where(pair => pair.Item.ProductId == product.ProductId)
                    .ToArray();
                var productVariants = variants.Where(variant => variant.ProductId == product.ProductId).ToArray();
                var returnedQuantity = returns
                    .SelectMany(returnRequest => returnRequest.Items)
                    .Where(item => item.ProductId == product.ProductId)
                    .Sum(item => item.Quantity);
                var unitsSold = orderItems.Sum(pair => pair.Item.Quantity);
                var refundedAmount = orderItems.Sum(pair =>
                {
                    if (!orderRefunds.TryGetValue(pair.Order.Id, out var orderRefundedAmount) || pair.Order.ItemsSubtotal <= 0)
                    {
                        return 0m;
                    }

                    return decimal.Round(orderRefundedAmount * (pair.Item.LineTotal / pair.Order.ItemsSubtotal), 2);
                });

                return new SellerProductPerformanceResponse(
                    product.ProductId,
                    product.Title,
                    product.Slug,
                    product.Status.ToString(),
                    unitsSold,
                    orderItems.Sum(pair => pair.Item.LineTotal),
                    refundedAmount,
                    returnedQuantity,
                    unitsSold == 0 ? 0 : decimal.Round((decimal)returnedQuantity / unitsSold, 4),
                    productVariants.Sum(variant => variant.StockQuantity),
                    productVariants.Sum(variant => variant.ReservedQuantity),
                    productVariants.Sum(variant => variant.StockQuantity - variant.ReservedQuantity));
            })
            .OrderByDescending(product => product.GrossSales)
            .ThenByDescending(product => product.UnitsSold)
            .ThenBy(product => product.ProductTitle)
            .ToArray();
    }

    private static IReadOnlyCollection<SellerInventoryPerformanceResponse> BuildInventoryPerformance(
        IReadOnlyCollection<ProductAnalyticsProjection> products,
        IReadOnlyCollection<VariantAnalyticsProjection> variants,
        IReadOnlyCollection<InventoryMovementLastActivityProjection> movements)
    {
        var productsById = products.ToDictionary(product => product.ProductId);
        var movementsByVariant = movements.ToDictionary(movement => movement.ProductVariantId, movement => movement.LastMovementAtUtc);

        return variants
            .Select(variant =>
            {
                productsById.TryGetValue(variant.ProductId, out var product);
                var available = variant.StockQuantity - variant.ReservedQuantity;

                return new SellerInventoryPerformanceResponse(
                    variant.ProductId,
                    product?.Title,
                    variant.ProductVariantId,
                    variant.Sku,
                    variant.Barcode,
                    variant.Size,
                    variant.Colour,
                    variant.Status.ToString(),
                    variant.StockQuantity,
                    variant.ReservedQuantity,
                    available,
                    available > 0 && available <= 5,
                    available <= 0 || variant.Status == ProductVariantStatus.OutOfStock,
                    movementsByVariant.GetValueOrDefault(variant.ProductVariantId, variant.UpdatedAtUtc));
            })
            .OrderBy(inventory => inventory.AvailableQuantity)
            .ThenBy(inventory => inventory.ProductTitle)
            .ThenBy(inventory => inventory.Sku)
            .ToArray();
    }

    private static async Task<IReadOnlyCollection<SellerAdPerformanceDetailResponse>> BuildAdPerformanceAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaigns = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.Name,
                campaign.Status
            })
            .ToListAsync(cancellationToken);
        var campaignIds = campaigns.Select(campaign => campaign.Id).ToArray();
        if (campaignIds.Length == 0)
        {
            return [];
        }

        var impressions = await dbContext.AdImpressions
            .AsNoTracking()
            .Where(impression => campaignIds.Contains(impression.AdCampaignId)
                && impression.OccurredAtUtc >= fromUtc
                && impression.OccurredAtUtc <= toUtc)
            .GroupBy(impression => impression.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Count, cancellationToken);
        var clicks = await dbContext.AdClicks
            .AsNoTracking()
            .Where(click => campaignIds.Contains(click.AdCampaignId)
                && click.OccurredAtUtc >= fromUtc
                && click.OccurredAtUtc <= toUtc)
            .GroupBy(click => click.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Count, cancellationToken);
        var spend = await dbContext.AdCharges
            .AsNoTracking()
            .Where(charge => campaignIds.Contains(charge.AdCampaignId)
                && charge.ChargedAtUtc >= fromUtc
                && charge.ChargedAtUtc <= toUtc)
            .GroupBy(charge => charge.AdCampaignId)
            .Select(group => new { CampaignId = group.Key, Amount = group.Sum(charge => charge.Amount) })
            .ToDictionaryAsync(group => group.CampaignId, group => group.Amount, cancellationToken);
        var conversions = await dbContext.AdConversions
            .AsNoTracking()
            .Where(conversion => campaignIds.Contains(conversion.AdCampaignId)
                && conversion.OccurredAtUtc >= fromUtc
                && conversion.OccurredAtUtc <= toUtc)
            .GroupBy(conversion => conversion.AdCampaignId)
            .Select(group => new
            {
                CampaignId = group.Key,
                OrderCount = group.Select(conversion => conversion.OrderId).Distinct().Count(),
                Revenue = group.Sum(conversion => conversion.RevenueAmount)
            })
            .ToDictionaryAsync(group => group.CampaignId, group => new { group.OrderCount, group.Revenue }, cancellationToken);

        return campaigns
            .Select(campaign =>
            {
                var campaignImpressions = impressions.GetValueOrDefault(campaign.Id);
                var campaignClicks = clicks.GetValueOrDefault(campaign.Id);
                var campaignSpend = spend.GetValueOrDefault(campaign.Id);
                conversions.TryGetValue(campaign.Id, out var campaignConversions);
                var revenue = campaignConversions?.Revenue ?? 0m;

                return new SellerAdPerformanceDetailResponse(
                    campaign.Id,
                    campaign.Name,
                    campaign.Status.ToString(),
                    campaignImpressions,
                    campaignClicks,
                    campaignImpressions == 0 ? 0 : decimal.Round((decimal)campaignClicks / campaignImpressions, 4),
                    campaignSpend,
                    campaignConversions?.OrderCount ?? 0,
                    revenue,
                    campaignSpend == 0 ? 0 : decimal.Round(revenue / campaignSpend, 4));
            })
            .OrderByDescending(campaign => campaign.RevenueGenerated)
            .ThenByDescending(campaign => campaign.Clicks)
            .ThenBy(campaign => campaign.Name)
            .ToArray();
    }

    private static async Task<SellerCustomerCareSummaryResponse> BuildCustomerCareSummaryAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<Refund> refunds,
        IReadOnlyCollection<ReturnRequest> returns,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var supportTickets = await dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.SellerId == sellerId
                && ticket.OpenedAtUtc >= fromUtc
                && ticket.OpenedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var disputes = await dbContext.Disputes
            .AsNoTracking()
            .Where(dispute => dispute.SellerId == sellerId
                && dispute.OpenedAtUtc >= fromUtc
                && dispute.OpenedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        return new SellerCustomerCareSummaryResponse(
            returns.Count,
            returns.Count(returnRequest => IsOpenReturnStatus(returnRequest.Status)),
            refunds.Count,
            refunds.Sum(refund => refund.Amount),
            supportTickets.Count,
            supportTickets.Count(ticket => !ticket.IsClosed),
            disputes.Count,
            disputes.Count(dispute => dispute.IsActive));
    }

    private static async Task<IReadOnlyCollection<SellerLowStockProductResponse>> GetLowStockProductsAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .Select(product => new { product.Id, product.Title, product.Status })
            .ToListAsync(cancellationToken);
        var productIds = products.Select(product => product.Id).ToArray();
        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId))
            .ToListAsync(cancellationToken);

        return products
            .Select(product =>
            {
                var productVariants = variants.Where(variant => variant.ProductId == product.Id).ToArray();
                return new SellerLowStockProductResponse(
                    product.Id,
                    product.Title,
                    product.Status.ToString(),
                    productVariants.Sum(variant => variant.StockQuantity - variant.ReservedQuantity),
                    productVariants.Count(variant => variant.StockQuantity - variant.ReservedQuantity <= 5));
            })
            .Where(product => product.AvailableQuantity <= 5 || product.LowStockVariantCount > 0)
            .OrderBy(product => product.AvailableQuantity)
            .ThenBy(product => product.Title)
            .Take(10)
            .ToArray();
    }

    private static async Task<SellerAdAnalyticsResponse> GetAdPerformanceAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var campaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .Select(campaign => campaign.Id)
            .ToListAsync(cancellationToken);
        if (campaignIds.Count == 0)
        {
            return new SellerAdAnalyticsResponse(0, 0, 0, 0, 0, 0, 0, []);
        }

        var campaignSummaries = new List<SellerAdCampaignAnalyticsResponse>();
        foreach (var campaignId in campaignIds)
        {
            var campaign = await dbContext.AdCampaigns.AsNoTracking().SingleAsync(campaign => campaign.Id == campaignId, cancellationToken);
            var impressions = await dbContext.AdImpressions.CountAsync(impression => impression.AdCampaignId == campaignId, cancellationToken);
            var clicks = await dbContext.AdClicks.CountAsync(click => click.AdCampaignId == campaignId, cancellationToken);
            var spend = await dbContext.AdCharges
                .Where(charge => charge.AdCampaignId == campaignId)
                .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
            var conversions = await dbContext.AdConversions
                .AsNoTracking()
                .Where(conversion => conversion.AdCampaignId == campaignId)
                .ToListAsync(cancellationToken);
            var revenue = conversions.Sum(conversion => conversion.RevenueAmount);
            campaignSummaries.Add(new SellerAdCampaignAnalyticsResponse(
                campaign.Id,
                campaign.Name,
                campaign.Status.ToString(),
                impressions,
                clicks,
                impressions == 0 ? 0 : decimal.Round((decimal)clicks / impressions, 4),
                spend,
                conversions.Select(conversion => conversion.OrderId).Distinct().Count(),
                revenue,
                spend == 0 ? 0 : decimal.Round(revenue / spend, 4)));
        }

        var totalImpressions = campaignSummaries.Sum(campaign => campaign.Impressions);
        var totalClicks = campaignSummaries.Sum(campaign => campaign.Clicks);
        var totalSpend = campaignSummaries.Sum(campaign => campaign.Spend);
        var totalRevenue = campaignSummaries.Sum(campaign => campaign.RevenueGenerated);

        return new SellerAdAnalyticsResponse(
            campaignSummaries.Count,
            totalImpressions,
            totalClicks,
            totalImpressions == 0 ? 0 : decimal.Round((decimal)totalClicks / totalImpressions, 4),
            totalSpend,
            campaignSummaries.Sum(campaign => campaign.OrdersGenerated),
            totalRevenue,
            campaignSummaries
                .OrderByDescending(campaign => campaign.RevenueGenerated)
                .ThenByDescending(campaign => campaign.Clicks)
                .Take(5)
                .ToArray());
    }

    private static async Task<SellerAiUsageAnalyticsResponse> GetAiUsageAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var usage = await dbContext.AiUsageLogs
            .AsNoTracking()
            .Where(log => log.SellerId == sellerId)
            .ToListAsync(cancellationToken);
        var suggestions = await dbContext.AiProductSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.SellerId == sellerId)
            .ToListAsync(cancellationToken);
        var appliedSuggestionIds = suggestions
            .Where(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied)
            .Select(suggestion => suggestion.Id)
            .ToArray();
        var fieldAudits = appliedSuggestionIds.Length == 0
            ? []
            : await dbContext.AiSuggestionFieldAudits
                .AsNoTracking()
                .Where(audit => appliedSuggestionIds.Contains(audit.SuggestionId))
                .ToListAsync(cancellationToken);
        var acceptedSuggestions = suggestions.Count(suggestion =>
            suggestion.Status is AiProductSuggestionStatus.Accepted or AiProductSuggestionStatus.Applied);

        return new SellerAiUsageAnalyticsResponse(
            usage.Count,
            usage.Count(log => log.Success),
            usage.Count(log => !log.Success),
            usage.Sum(log => log.CostEstimate ?? 0),
            usage.Count == 0 ? 0 : decimal.Round((decimal)usage.Average(log => log.LatencyMs), 2),
            suggestions.Count,
            acceptedSuggestions,
            suggestions.Count == 0 ? 0 : decimal.Round((decimal)acceptedSuggestions / suggestions.Count, 4),
            suggestions
                .Where(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied)
                .Select(suggestion => suggestion.ProductId)
                .Distinct()
                .Count(),
            suggestions.Count == 0 ? 0 : decimal.Round(suggestions.Average(suggestion => suggestion.QualityScore), 2),
            null,
            "Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.",
            fieldAudits.Count(audit => audit.WasAccepted),
            fieldAudits.Count(audit => audit.WasEdited));
    }

    private static IReadOnlyCollection<AnalyticsBucket> BuildBuckets(DateTimeOffset fromUtc, DateTimeOffset toUtc, AnalyticsBucketKind bucket)
    {
        var buckets = new List<AnalyticsBucket>();
        var cursor = fromUtc;
        var step = bucket == AnalyticsBucketKind.Week ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);

        while (cursor <= toUtc)
        {
            var next = cursor.Add(step);
            buckets.Add(new AnalyticsBucket(cursor, next > toUtc ? toUtc : next));
            cursor = next;
        }

        return buckets;
    }

    private static bool IsInBucket(DateTimeOffset value, DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset rangeEndUtc) =>
        value >= startUtc && (endUtc == rangeEndUtc ? value <= endUtc : value < endUtc);

    private static ReportDateRange ResolveRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, TimeProvider timeProvider)
    {
        var resolvedTo = (toUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var resolvedFrom = (fromUtc ?? resolvedTo.AddDays(-30)).ToUniversalTime();
        var isValid = resolvedFrom <= resolvedTo && (resolvedTo - resolvedFrom) <= TimeSpan.FromDays(MaxRangeDays);

        return new ReportDateRange(resolvedFrom, resolvedTo, isValid);
    }

    private static bool TryResolveBucket(string? bucket, out AnalyticsBucketKind bucketKind)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            bucketKind = AnalyticsBucketKind.Day;
            return true;
        }

        return Enum.TryParse(bucket, ignoreCase: true, out bucketKind)
            && Enum.IsDefined(bucketKind);
    }

    private static bool TryResolveReport(string? report, out SellerAnalyticsCsvReport reportKind)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            reportKind = SellerAnalyticsCsvReport.Sales;
            return true;
        }

        return Enum.TryParse(report, ignoreCase: true, out reportKind)
            && Enum.IsDefined(reportKind);
    }

    private static bool TryResolveSourceCategory(string? sourceCategory, out string? normalizedSourceCategory)
    {
        normalizedSourceCategory = null;
        if (string.IsNullOrWhiteSpace(sourceCategory))
        {
            return true;
        }

        normalizedSourceCategory = FunnelSourceCategories.FirstOrDefault(category =>
            string.Equals(category, sourceCategory.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalizedSourceCategory is not null;
    }

    private static bool IsSalesOrderStatus(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.Processing
            or OrderStatus.ReadyToShip
            or OrderStatus.Shipped
            or OrderStatus.Delivered
            or OrderStatus.ReturnRequested
            or OrderStatus.Disputed
            or OrderStatus.Completed;

    private static bool IsOpenReturnStatus(ReturnStatus status) =>
        status is ReturnStatus.Requested
            or ReturnStatus.AwaitingSellerResponse
            or ReturnStatus.Approved
            or ReturnStatus.ReturnInTransit
            or ReturnStatus.ReturnedToSeller
            or ReturnStatus.RefundPending
            or ReturnStatus.Disputed;

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
            title: "SellerAnalytics.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult InvalidRangeProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidDateRange",
            detail: $"fromUtc must be earlier than or equal to toUtc, and the range cannot exceed {MaxRangeDays} days.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidBucketProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidBucket",
            detail: "bucket must be Day or Week.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidReportProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidReport",
            detail: "report must be Sales, Products, Inventory, Ads, Returns, or Funnel.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidSourceCategoryProblem() =>
        HttpResults.Problem(
            title: "SellerAnalytics.InvalidSourceCategory",
            detail: "sourceCategory must be Direct, Search, Social, Email, Ads, Referral, or Unknown.",
            statusCode: StatusCodes.Status400BadRequest);

    private static string BuildCsv(SellerAnalyticsPerformanceResponse analytics, SellerAnalyticsCsvReport report) =>
        report switch
        {
            SellerAnalyticsCsvReport.Sales => BuildSalesCsv(analytics.SalesTrend),
            SellerAnalyticsCsvReport.Products => BuildProductsCsv(analytics.ProductPerformance),
            SellerAnalyticsCsvReport.Inventory => BuildInventoryCsv(analytics.InventoryPerformance),
            SellerAnalyticsCsvReport.Ads => BuildAdsCsv(analytics.AdPerformance),
            SellerAnalyticsCsvReport.Returns => BuildReturnsCsv(analytics.CustomerCareSummary),
            SellerAnalyticsCsvReport.Funnel => BuildFunnelCsv(analytics.ProductFunnel),
            _ => string.Empty
        };

    private static string BuildSalesCsv(IReadOnlyCollection<SellerSalesTrendBucketResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "periodStartUtc", "periodEndUtc", "orderCount", "grossSales", "refundedAmount", "netSales", "unitsSold", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.PeriodStartUtc.ToString("O", CultureInfo.InvariantCulture),
                row.PeriodEndUtc.ToString("O", CultureInfo.InvariantCulture),
                row.OrderCount.ToString(CultureInfo.InvariantCulture),
                row.GrossSales.ToString(CultureInfo.InvariantCulture),
                row.RefundedAmount.ToString(CultureInfo.InvariantCulture),
                row.NetSales.ToString(CultureInfo.InvariantCulture),
                row.UnitsSold.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildProductsCsv(IReadOnlyCollection<SellerProductPerformanceResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "productId", "productTitle", "productSlug", "status", "unitsSold", "grossSales", "refundedAmount", "returnCount", "returnRate", "stockQuantity", "reservedQuantity", "availableQuantity", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.ProductId.ToString(),
                row.ProductTitle ?? string.Empty,
                row.ProductSlug ?? string.Empty,
                row.Status,
                row.UnitsSold.ToString(CultureInfo.InvariantCulture),
                row.GrossSales.ToString(CultureInfo.InvariantCulture),
                row.RefundedAmount.ToString(CultureInfo.InvariantCulture),
                row.ReturnCount.ToString(CultureInfo.InvariantCulture),
                row.ReturnRate.ToString(CultureInfo.InvariantCulture),
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                row.ReservedQuantity.ToString(CultureInfo.InvariantCulture),
                row.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildInventoryCsv(IReadOnlyCollection<SellerInventoryPerformanceResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "productId", "productTitle", "productVariantId", "sku", "barcode", "size", "colour", "status", "stockQuantity", "reservedQuantity", "availableQuantity", "isLowStock", "isOutOfStock", "lastMovementAtUtc");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.ProductId.ToString(),
                row.ProductTitle ?? string.Empty,
                row.ProductVariantId.ToString(),
                row.Sku,
                row.Barcode ?? string.Empty,
                row.Size,
                row.Colour,
                row.Status,
                row.StockQuantity.ToString(CultureInfo.InvariantCulture),
                row.ReservedQuantity.ToString(CultureInfo.InvariantCulture),
                row.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                row.IsLowStock.ToString(CultureInfo.InvariantCulture),
                row.IsOutOfStock.ToString(CultureInfo.InvariantCulture),
                row.LastMovementAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return builder.ToString();
    }

    private static string BuildAdsCsv(IReadOnlyCollection<SellerAdPerformanceDetailResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "adCampaignId", "name", "status", "impressions", "clicks", "clickThroughRate", "spend", "ordersGenerated", "revenueGenerated", "returnOnAdSpend", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.AdCampaignId.ToString(),
                row.Name,
                row.Status,
                row.Impressions.ToString(CultureInfo.InvariantCulture),
                row.Clicks.ToString(CultureInfo.InvariantCulture),
                row.ClickThroughRate.ToString(CultureInfo.InvariantCulture),
                row.Spend.ToString(CultureInfo.InvariantCulture),
                row.OrdersGenerated.ToString(CultureInfo.InvariantCulture),
                row.RevenueGenerated.ToString(CultureInfo.InvariantCulture),
                row.ReturnOnAdSpend.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static string BuildReturnsCsv(SellerCustomerCareSummaryResponse summary)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "returnCount", "openReturnCount", "refundCount", "refundedAmount", "supportTicketCount", "openSupportTicketCount", "disputeCount", "activeDisputeCount", "currency");
        AppendCsvLine(
            builder,
            summary.ReturnCount.ToString(CultureInfo.InvariantCulture),
            summary.OpenReturnCount.ToString(CultureInfo.InvariantCulture),
            summary.RefundCount.ToString(CultureInfo.InvariantCulture),
            summary.RefundedAmount.ToString(CultureInfo.InvariantCulture),
            summary.SupportTicketCount.ToString(CultureInfo.InvariantCulture),
            summary.OpenSupportTicketCount.ToString(CultureInfo.InvariantCulture),
            summary.DisputeCount.ToString(CultureInfo.InvariantCulture),
            summary.ActiveDisputeCount.ToString(CultureInfo.InvariantCulture),
            DefaultCurrency);
        return builder.ToString();
    }

    private static string BuildFunnelCsv(IReadOnlyCollection<SellerProductFunnelResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "productId", "productTitle", "productSlug", "dominantSourceCategory", "topUtmSource", "topReferrerHost", "productViews", "addToCartCount", "paidOrderCount", "revenue", "productViewToCartRate", "productViewToPaidRate", "currency");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.ProductId.ToString(),
                row.ProductTitle ?? string.Empty,
                row.ProductSlug ?? string.Empty,
                row.DominantSourceCategory,
                row.TopUtmSource ?? string.Empty,
                row.TopReferrerHost ?? string.Empty,
                row.ProductViews.ToString(CultureInfo.InvariantCulture),
                row.AddToCartCount.ToString(CultureInfo.InvariantCulture),
                row.PaidOrderCount.ToString(CultureInfo.InvariantCulture),
                row.Revenue.ToString(CultureInfo.InvariantCulture),
                row.ProductViewToCartRate.ToString(CultureInfo.InvariantCulture),
                row.ProductViewToPaidRate.ToString(CultureInfo.InvariantCulture),
                DefaultCurrency);
        }

        return builder.ToString();
    }

    private static void AppendCsvLine(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(Csv)));
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private enum AnalyticsBucketKind
    {
        Day,
        Week
    }

    private enum SellerAnalyticsCsvReport
    {
        Sales,
        Products,
        Inventory,
        Ads,
        Returns,
        Funnel
    }

    private sealed record ReportDateRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc, bool IsValid);

    private sealed record AnalyticsBucket(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);

    private sealed record ProductAnalyticsProjection(
        Guid ProductId,
        string? Title,
        string? Slug,
        ProductStatus Status);

    private sealed record VariantAnalyticsProjection(
        Guid ProductVariantId,
        Guid ProductId,
        string Sku,
        string? Barcode,
        string Size,
        string Colour,
        ProductVariantStatus Status,
        int StockQuantity,
        int ReservedQuantity,
        DateTimeOffset UpdatedAtUtc);

    private sealed record InventoryMovementLastActivityProjection(
        Guid ProductVariantId,
        DateTimeOffset LastMovementAtUtc);
}

public sealed record SellerAnalyticsSummaryResponse(
    Guid SellerId,
    decimal TotalSales,
    int OrderCount,
    decimal AverageOrderValue,
    decimal ConversionRatePlaceholder,
    int ProductsSold,
    decimal TotalRefunded,
    decimal RefundRate,
    decimal ReturnRate,
    IReadOnlyCollection<SellerTopProductResponse> TopProducts,
    IReadOnlyCollection<SellerLowStockProductResponse> LowStockProducts,
    SellerAdAnalyticsResponse AdPerformance,
    SellerAiUsageAnalyticsResponse AiUsage);

public sealed record SellerTopProductResponse(
    Guid ProductId,
    string? ProductTitle,
    int QuantitySold,
    decimal Revenue);

public sealed record SellerLowStockProductResponse(
    Guid ProductId,
    string? Title,
    string Status,
    int AvailableQuantity,
    int LowStockVariantCount);

public sealed record SellerAdAnalyticsResponse(
    int CampaignCount,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    IReadOnlyCollection<SellerAdCampaignAnalyticsResponse> TopCampaigns);

public sealed record SellerAdCampaignAnalyticsResponse(
    Guid AdCampaignId,
    string Name,
    string Status,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    decimal ReturnOnAdSpend);

public sealed record SellerAiUsageAnalyticsResponse(
    int Requests,
    int SuccessfulRequests,
    int FailedRequests,
    decimal EstimatedCost,
    decimal AverageLatencyMs,
    int SuggestionsGenerated,
    int SuggestionsAccepted,
    decimal SuggestionAcceptanceRate,
    int ProductsImprovedWithAi,
    decimal AverageListingQualityScore,
    decimal? AverageQualityScoreImprovement,
    string QualityScoreImprovementNote,
    int FieldValuesAccepted,
    int FieldValuesEdited);

public sealed record SellerAnalyticsPerformanceResponse(
    Guid SellerId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Bucket,
    IReadOnlyCollection<SellerSalesTrendBucketResponse> SalesTrend,
    IReadOnlyCollection<SellerProductPerformanceResponse> ProductPerformance,
    IReadOnlyCollection<SellerInventoryPerformanceResponse> InventoryPerformance,
    IReadOnlyCollection<SellerAdPerformanceDetailResponse> AdPerformance,
    SellerCustomerCareSummaryResponse CustomerCareSummary,
    SellerFunnelSummaryResponse FunnelSummary,
    IReadOnlyCollection<SellerFunnelTrendBucketResponse> FunnelTrend,
    IReadOnlyCollection<SellerProductFunnelResponse> ProductFunnel,
    IReadOnlyCollection<SellerFunnelSourceBreakdownResponse> SourceBreakdown);

public sealed record SellerSalesTrendBucketResponse(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int OrderCount,
    decimal GrossSales,
    decimal RefundedAmount,
    decimal NetSales,
    int UnitsSold);

public sealed record SellerProductPerformanceResponse(
    Guid ProductId,
    string? ProductTitle,
    string? ProductSlug,
    string Status,
    int UnitsSold,
    decimal GrossSales,
    decimal RefundedAmount,
    int ReturnCount,
    decimal ReturnRate,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity);

public sealed record SellerInventoryPerformanceResponse(
    Guid ProductId,
    string? ProductTitle,
    Guid ProductVariantId,
    string Sku,
    string? Barcode,
    string Size,
    string Colour,
    string Status,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    bool IsLowStock,
    bool IsOutOfStock,
    DateTimeOffset? LastMovementAtUtc);

public sealed record SellerAdPerformanceDetailResponse(
    Guid AdCampaignId,
    string Name,
    string Status,
    int Impressions,
    int Clicks,
    decimal ClickThroughRate,
    decimal Spend,
    int OrdersGenerated,
    decimal RevenueGenerated,
    decimal ReturnOnAdSpend);

public sealed record SellerCustomerCareSummaryResponse(
    int ReturnCount,
    int OpenReturnCount,
    int RefundCount,
    decimal RefundedAmount,
    int SupportTicketCount,
    int OpenSupportTicketCount,
    int DisputeCount,
    int ActiveDisputeCount);

public sealed record SellerFunnelSummaryResponse(
    int StorefrontViews,
    int ProductViews,
    int AddToCartCount,
    int CheckoutStartCount,
    int OrderCreatedCount,
    int PaidOrderCount,
    decimal ProductViewToCartRate,
    decimal CheckoutToPaidRate);

public sealed record SellerFunnelTrendBucketResponse(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int StorefrontViews,
    int ProductViews,
    int AddToCartCount,
    int CheckoutStartCount,
    int OrderCreatedCount,
    int PaidOrderCount,
    decimal ProductViewToCartRate,
    decimal CheckoutToPaidRate);

public sealed record SellerProductFunnelResponse(
    Guid ProductId,
    string? ProductTitle,
    string? ProductSlug,
    int ProductViews,
    int AddToCartCount,
    int PaidOrderCount,
    decimal Revenue,
    decimal ProductViewToCartRate,
    decimal ProductViewToPaidRate,
    string DominantSourceCategory,
    string? TopUtmSource,
    string? TopReferrerHost);

public sealed record SellerFunnelSourceBreakdownResponse(
    string SourceCategory,
    int StorefrontViews,
    int ProductViews,
    int AddToCartCount,
    int CheckoutStartCount,
    int OrderCreatedCount,
    int PaidOrderCount,
    decimal ProductViewToCartRate,
    decimal CheckoutToPaidRate,
    string? TopUtmSource,
    string? TopReferrerHost);
