using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Domain.Support;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerDashboardEndpoints
{
    private const int LowStockThreshold = 5;

    public static IEndpointRouteBuilder MapSellerDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/seller/dashboard/summary", GetSummaryAsync)
            .WithTags("Seller Dashboard")
            .WithName("GetSellerDashboardSummary")
            .WithSummary("Returns live seller-owned operational counts, alerts, and recent activity.")
            .RequireAuthorization(MabuntlePolicies.SellerOnly)
            .Produces<SellerDashboardSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
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

        if (seller.VerificationStatus != SellerVerificationStatus.Verified)
        {
            return SellerNotVerified();
        }

        var now = timeProvider.GetUtcNow();
        var fromUtc = now.AddDays(-30);

        var salesStatuses = new[]
        {
            OrderStatus.Paid,
            OrderStatus.Processing,
            OrderStatus.ReadyToShip,
            OrderStatus.Shipped,
            OrderStatus.Delivered,
            OrderStatus.ReturnRequested,
            OrderStatus.Disputed,
            OrderStatus.Completed
        };

        var recentSalesOrders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == seller.Id
                && order.CreatedAtUtc >= fromUtc
                && salesStatuses.Contains(order.Status))
            .ToListAsync(cancellationToken);
        var salesLast30Days = recentSalesOrders.Sum(order => order.TotalAmount);
        var ordersLast30Days = recentSalesOrders.Count;

        var paidOrderCount = await CountOrdersAsync(dbContext, seller.Id, OrderStatus.Paid, cancellationToken);
        var processingOrderCount = await CountOrdersAsync(dbContext, seller.Id, OrderStatus.Processing, cancellationToken);
        var readyToShipOrderCount = await CountOrdersAsync(dbContext, seller.Id, OrderStatus.ReadyToShip, cancellationToken);
        var shippedOrderCount = await CountOrdersAsync(dbContext, seller.Id, OrderStatus.Shipped, cancellationToken);
        var deliveryExceptionOrderCount = await dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.SellerId == seller.Id
                && (shipment.Status == ShipmentStatus.DeliveryFailed || shipment.Status == ShipmentStatus.ReturnedToSender))
            .Select(shipment => shipment.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);
        var pendingFulfilmentOrders = paidOrderCount + processingOrderCount + readyToShipOrderCount;

        var draftProductCount = await CountProductsAsync(dbContext, seller.Id, ProductStatus.Draft, cancellationToken);
        var pendingReviewProductCount = await dbContext.Products
            .AsNoTracking()
            .CountAsync(
                product => product.SellerId == seller.Id
                    && (product.Status == ProductStatus.PendingReview || product.Status == ProductStatus.NeedsAdminReview),
                cancellationToken);
        var publishedProductCount = await CountProductsAsync(dbContext, seller.Id, ProductStatus.Published, cancellationToken);
        var changesRequestedProductCount = await CountProductsAsync(dbContext, seller.Id, ProductStatus.ChangesRequested, cancellationToken);
        var pendingListingRevisionCount = await dbContext.ProductListingRevisions
            .AsNoTracking()
            .CountAsync(
                revision => revision.SellerId == seller.Id && revision.Status == ProductListingRevisionStatus.PendingReview,
                cancellationToken);
        var pendingVariantRevisionCount = await dbContext.ProductVariantRevisions
            .AsNoTracking()
            .CountAsync(
                revision => revision.SellerId == seller.Id && revision.Status == ProductVariantRevisionStatus.PendingReview,
                cancellationToken);

        var productIds = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == seller.Id)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var inventoryMetrics = await GetInventoryMetricsAsync(productIds, dbContext, cancellationToken);

        var openReturnStatuses = new[]
        {
            ReturnStatus.Requested,
            ReturnStatus.AwaitingSellerResponse,
            ReturnStatus.Approved,
            ReturnStatus.ReturnInTransit,
            ReturnStatus.ReturnedToSeller,
            ReturnStatus.RefundPending,
            ReturnStatus.Disputed
        };
        var openReturnCount = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(
                returnRequest => returnRequest.SellerId == seller.Id && openReturnStatuses.Contains(returnRequest.Status),
                cancellationToken);
        var returnsAwaitingSellerResponseCount = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(
                returnRequest => returnRequest.SellerId == seller.Id
                    && (returnRequest.Status == ReturnStatus.Requested
                        || returnRequest.Status == ReturnStatus.AwaitingSellerResponse),
                cancellationToken);

        var openSupportStatuses = new[]
        {
            SupportTicketStatus.Open,
            SupportTicketStatus.WaitingForSeller,
            SupportTicketStatus.WaitingForCustomer,
            SupportTicketStatus.Escalated
        };
        var openSupportTicketCount = await dbContext.SupportTickets
            .AsNoTracking()
            .CountAsync(
                ticket => ticket.SellerId == seller.Id && openSupportStatuses.Contains(ticket.Status),
                cancellationToken);
        var activeDisputeStatuses = new[]
        {
            DisputeStatus.Open,
            DisputeStatus.AwaitingBuyer,
            DisputeStatus.AwaitingSeller,
            DisputeStatus.UnderAdminReview
        };
        var activeDisputeCount = await dbContext.Disputes
            .AsNoTracking()
            .CountAsync(
                dispute => dispute.SellerId == seller.Id && activeDisputeStatuses.Contains(dispute.Status),
                cancellationToken);

        var pendingPayoutAmount = await dbContext.SellerBalances
            .AsNoTracking()
            .Where(balance => balance.SellerId == seller.Id)
            .SumAsync(balance => (decimal?)balance.PendingBalance, cancellationToken) ?? 0m;
        var availablePayoutAmount = await dbContext.SellerBalances
            .AsNoTracking()
            .Where(balance => balance.SellerId == seller.Id)
            .SumAsync(balance => (decimal?)balance.AvailableBalance, cancellationToken) ?? 0m;
        var heldPayoutAmount = await dbContext.SellerBalances
            .AsNoTracking()
            .Where(balance => balance.SellerId == seller.Id)
            .SumAsync(balance => (decimal?)balance.HeldBalance, cancellationToken) ?? 0m;
        var pendingPayoutCount = await dbContext.SellerPayouts
            .AsNoTracking()
            .CountAsync(
                payout => payout.SellerId == seller.Id && payout.Status == SellerPayoutStatus.Pending,
                cancellationToken);
        var processingPayoutCount = await dbContext.SellerPayouts
            .AsNoTracking()
            .CountAsync(
                payout => payout.SellerId == seller.Id && payout.Status == SellerPayoutStatus.Processing,
                cancellationToken);
        var hasPendingPayoutProfileChange = await dbContext.SellerPayoutProfileChangeRequests
            .AsNoTracking()
            .AnyAsync(
                request => request.SellerId == seller.Id
                    && request.Status == SellerPayoutProfileChangeRequestStatus.PendingReview,
                cancellationToken);

        var activeAdCampaignCount = await dbContext.AdCampaigns
            .AsNoTracking()
            .CountAsync(
                campaign => campaign.SellerId == seller.Id && campaign.Status == AdCampaignStatus.Active,
                cancellationToken);
        var pendingAdReviewCount = await dbContext.AdCampaigns
            .AsNoTracking()
            .CountAsync(
                campaign => campaign.SellerId == seller.Id && campaign.Status == AdCampaignStatus.PendingReview,
                cancellationToken);
        var adCampaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == seller.Id)
            .Select(campaign => campaign.Id)
            .ToListAsync(cancellationToken);
        var adSpendLast30Days = adCampaignIds.Count == 0
            ? 0m
            : await dbContext.AdCharges
                .AsNoTracking()
                .Where(charge => adCampaignIds.Contains(charge.AdCampaignId) && charge.ChargedAtUtc >= fromUtc)
                .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
        var adRevenueLast30Days = adCampaignIds.Count == 0
            ? 0m
            : await dbContext.AdConversions
                .AsNoTracking()
                .Where(conversion => adCampaignIds.Contains(conversion.AdCampaignId) && conversion.OccurredAtUtc >= fromUtc)
                .SumAsync(conversion => (decimal?)conversion.RevenueAmount, cancellationToken) ?? 0m;

        var unreadNotificationCount = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == seller.UserId
                    && notification.IsInAppVisible
                    && notification.ReadAtUtc == null,
                cancellationToken);

        var alerts = BuildAlerts(
            pendingFulfilmentOrders,
            deliveryExceptionOrderCount,
            inventoryMetrics.LowStockProductCount,
            inventoryMetrics.OutOfStockVariantCount,
            returnsAwaitingSellerResponseCount,
            openSupportTicketCount,
            activeDisputeCount,
            pendingReviewProductCount,
            pendingListingRevisionCount,
            pendingVariantRevisionCount,
            hasPendingPayoutProfileChange,
            pendingAdReviewCount,
            unreadNotificationCount);
        var recentActivity = await GetRecentActivityAsync(seller.Id, dbContext, cancellationToken);

        return HttpResults.Ok(new SellerDashboardSummaryResponse(
            seller.Id,
            now,
            fromUtc,
            decimal.Round(salesLast30Days, 2),
            ordersLast30Days,
            paidOrderCount,
            processingOrderCount,
            readyToShipOrderCount,
            shippedOrderCount,
            pendingFulfilmentOrders,
            deliveryExceptionOrderCount,
            draftProductCount,
            pendingReviewProductCount,
            publishedProductCount,
            changesRequestedProductCount,
            pendingListingRevisionCount,
            pendingVariantRevisionCount,
            inventoryMetrics.LowStockProductCount,
            inventoryMetrics.OutOfStockVariantCount,
            inventoryMetrics.ReservedStockCount,
            openReturnCount,
            returnsAwaitingSellerResponseCount,
            openSupportTicketCount,
            activeDisputeCount,
            decimal.Round(pendingPayoutAmount, 2),
            decimal.Round(availablePayoutAmount, 2),
            decimal.Round(heldPayoutAmount, 2),
            pendingPayoutCount,
            processingPayoutCount,
            hasPendingPayoutProfileChange,
            activeAdCampaignCount,
            pendingAdReviewCount,
            decimal.Round(adSpendLast30Days, 2),
            decimal.Round(adRevenueLast30Days, 2),
            unreadNotificationCount,
            alerts,
            recentActivity));
    }

    private static async Task<SellerInventoryMetrics> GetInventoryMetricsAsync(
        IReadOnlyCollection<Guid> productIds,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new SellerInventoryMetrics(0, 0, 0);
        }

        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId))
            .Select(variant => new
            {
                variant.ProductId,
                variant.Status,
                variant.StockQuantity,
                variant.ReservedQuantity
            })
            .ToListAsync(cancellationToken);

        var lowStockProductCount = variants
            .Where(variant => variant.Status == ProductVariantStatus.Active)
            .GroupBy(variant => variant.ProductId)
            .Count(group => group.Any(variant =>
            {
                var available = variant.StockQuantity - variant.ReservedQuantity;
                return available > 0 && available <= LowStockThreshold;
            }));
        var outOfStockVariantCount = variants.Count(variant =>
            variant.Status == ProductVariantStatus.OutOfStock
                || variant.StockQuantity - variant.ReservedQuantity <= 0);
        var reservedStockCount = variants.Sum(variant => variant.ReservedQuantity);

        return new SellerInventoryMetrics(lowStockProductCount, outOfStockVariantCount, reservedStockCount);
    }

    private static async Task<IReadOnlyCollection<SellerDashboardActivityResponse>> GetRecentActivityAsync(
        Guid sellerId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var activities = new List<SellerDashboardActivityResponse>();

        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.SellerId == sellerId)
            .OrderByDescending(order => order.UpdatedAtUtc)
            .Take(5)
            .Select(order => new
            {
                order.Id,
                order.Status,
                order.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(orders.Select(order => new SellerDashboardActivityResponse(
            "Order",
            $"Order {ShortId(order.Id)}",
            order.Status.ToString(),
            order.UpdatedAtUtc,
            $"/seller/orders/{order.Id}")));

        var returns = await dbContext.ReturnRequests
            .AsNoTracking()
            .Where(returnRequest => returnRequest.SellerId == sellerId)
            .OrderByDescending(returnRequest => returnRequest.UpdatedAtUtc)
            .Take(5)
            .Select(returnRequest => new
            {
                returnRequest.Id,
                returnRequest.Status,
                returnRequest.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(returns.Select(returnRequest => new SellerDashboardActivityResponse(
            "Return",
            $"Return {ShortId(returnRequest.Id)}",
            returnRequest.Status.ToString(),
            returnRequest.UpdatedAtUtc,
            $"/seller/returns/{returnRequest.Id}")));

        var supportTickets = await dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.SellerId == sellerId)
            .OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .Take(5)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.Subject,
                ticket.Status,
                ticket.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(supportTickets.Select(ticket => new SellerDashboardActivityResponse(
            "Support",
            ticket.Subject,
            ticket.Status.ToString(),
            ticket.UpdatedAtUtc,
            $"/seller/support/{ticket.Id}")));

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .OrderByDescending(product => product.UpdatedAtUtc)
            .Take(5)
            .Select(product => new
            {
                product.Id,
                product.Title,
                product.Status,
                product.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(products.Select(product => new SellerDashboardActivityResponse(
            "Product",
            product.Title ?? $"Product {ShortId(product.Id)}",
            product.Status.ToString(),
            product.UpdatedAtUtc,
            $"/seller/products/{product.Id}/edit")));

        var ads = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .OrderByDescending(campaign => campaign.UpdatedAtUtc)
            .Take(5)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.Name,
                campaign.Status,
                campaign.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(ads.Select(campaign => new SellerDashboardActivityResponse(
            "Ad",
            campaign.Name,
            campaign.Status.ToString(),
            campaign.UpdatedAtUtc,
            $"/seller/ads/{campaign.Id}")));

        return activities
            .OrderByDescending(activity => activity.OccurredAtUtc)
            .Take(8)
            .ToArray();
    }

    private static IReadOnlyCollection<SellerDashboardAlertResponse> BuildAlerts(
        int pendingFulfilmentOrders,
        int deliveryExceptionOrderCount,
        int lowStockProductCount,
        int outOfStockVariantCount,
        int returnsAwaitingSellerResponseCount,
        int openSupportTicketCount,
        int activeDisputeCount,
        int pendingReviewProductCount,
        int pendingListingRevisionCount,
        int pendingVariantRevisionCount,
        bool hasPendingPayoutProfileChange,
        int pendingAdReviewCount,
        int unreadNotificationCount)
    {
        var alerts = new List<SellerDashboardAlertResponse>();

        AddIf(alerts, deliveryExceptionOrderCount, "danger", "Delivery exceptions need review", "Failed or returned shipments need seller follow-up.", "/seller/orders");
        AddIf(alerts, pendingFulfilmentOrders, "warning", "Orders need fulfilment", "Paid, processing, or ready-to-ship orders are waiting in the fulfilment queue.", "/seller/orders");
        AddIf(alerts, lowStockProductCount, "warning", "Low-stock products", "Some published products have variants at or below the stock threshold.", "/seller/inventory");
        AddIf(alerts, outOfStockVariantCount, "warning", "Out-of-stock variants", "Restock or deactivate unavailable variants before buyers hit dead ends.", "/seller/inventory");
        AddIf(alerts, returnsAwaitingSellerResponseCount, "warning", "Returns awaiting response", "Buyer return requests need a seller decision.", "/seller/returns");
        AddIf(alerts, openSupportTicketCount, "warning", "Open support tickets", "Seller support threads are still open or escalated.", "/seller/support");
        AddIf(alerts, activeDisputeCount, "danger", "Active disputes", "Disputes are open and may need evidence or support follow-up.", "/seller/returns");
        AddIf(alerts, pendingReviewProductCount, "accent", "Products in moderation", "Submitted products are awaiting admin review.", "/seller/products");
        AddIf(alerts, pendingListingRevisionCount + pendingVariantRevisionCount, "accent", "Published changes in review", "Listing or variant changes are pending admin approval.", "/seller/products");
        AddIf(alerts, pendingAdReviewCount, "accent", "Ads in review", "Submitted ad campaigns are waiting for admin review.", "/seller/ads");

        if (hasPendingPayoutProfileChange)
        {
            alerts.Add(new SellerDashboardAlertResponse(
                "warning",
                "Payout profile change pending",
                "Finance review is pending; payout processing may be blocked until approval.",
                "/seller/settings/store",
                1));
        }

        AddIf(alerts, unreadNotificationCount, "neutral", "Unread notifications", "Review recent moderation, payout, and operation updates.", "/seller/notifications");

        return alerts.ToArray();
    }

    private static void AddIf(
        List<SellerDashboardAlertResponse> alerts,
        int count,
        string severity,
        string title,
        string message,
        string route)
    {
        if (count <= 0)
        {
            return;
        }

        alerts.Add(new SellerDashboardAlertResponse(severity, title, message, route, count));
    }

    private static async Task<int> CountOrdersAsync(
        MabuntleDbContext dbContext,
        Guid sellerId,
        OrderStatus status,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order => order.SellerId == sellerId && order.Status == status, cancellationToken);

    private static async Task<int> CountProductsAsync(
        MabuntleDbContext dbContext,
        Guid sellerId,
        ProductStatus status,
        CancellationToken cancellationToken) =>
        await dbContext.Products
            .AsNoTracking()
            .CountAsync(product => product.SellerId == sellerId && product.Status == status, cancellationToken);

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

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "SellerDashboard.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult SellerNotVerified() =>
        HttpResults.Problem(
            title: "SellerDashboard.NotVerified",
            detail: "Only verified sellers can access the operational dashboard summary.",
            statusCode: StatusCodes.Status409Conflict);

    private sealed record SellerInventoryMetrics(
        int LowStockProductCount,
        int OutOfStockVariantCount,
        int ReservedStockCount);
}

public sealed record SellerDashboardSummaryResponse(
    Guid SellerId,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FromUtc,
    decimal SalesLast30Days,
    int OrdersLast30Days,
    int PaidOrderCount,
    int ProcessingOrderCount,
    int ReadyToShipOrderCount,
    int ShippedOrderCount,
    int PendingFulfilmentOrders,
    int DeliveryExceptionOrderCount,
    int DraftProductCount,
    int PendingReviewProductCount,
    int PublishedProductCount,
    int ChangesRequestedProductCount,
    int PendingListingRevisionCount,
    int PendingVariantRevisionCount,
    int LowStockProductCount,
    int OutOfStockVariantCount,
    int ReservedStockCount,
    int OpenReturnCount,
    int ReturnsAwaitingSellerResponseCount,
    int OpenSupportTicketCount,
    int ActiveDisputeCount,
    decimal PendingPayoutAmount,
    decimal AvailablePayoutAmount,
    decimal HeldPayoutAmount,
    int PendingPayoutCount,
    int ProcessingPayoutCount,
    bool HasPendingPayoutProfileChange,
    int ActiveAdCampaignCount,
    int PendingAdReviewCount,
    decimal AdSpendLast30Days,
    decimal AdRevenueLast30Days,
    int UnreadNotificationCount,
    IReadOnlyCollection<SellerDashboardAlertResponse> Alerts,
    IReadOnlyCollection<SellerDashboardActivityResponse> RecentActivity);

public sealed record SellerDashboardAlertResponse(
    string Severity,
    string Title,
    string Message,
    string Route,
    int Count);

public sealed record SellerDashboardActivityResponse(
    string Type,
    string Title,
    string Status,
    DateTimeOffset OccurredAtUtc,
    string Route);
