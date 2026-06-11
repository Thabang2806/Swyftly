using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Disputes;
using Mabuntle.Domain.Ledger;
using Mabuntle.Domain.Refunds;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/dashboard")
            .WithTags("Admin Dashboard")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetAdminDashboardSummary")
            .WithSummary("Returns admin dashboard aggregate counts without exposing buyer or seller detail.")
            .Produces<AdminDashboardSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow();
        var todayStartUtc = new DateTimeOffset(utcNow.UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowStartUtc = todayStartUtc.AddDays(1);

        var pendingSellerApprovals = await dbContext.SellerProfiles
            .AsNoTracking()
            .CountAsync(seller => seller.VerificationStatus == SellerVerificationStatus.UnderReview, cancellationToken);

        var pendingProductReviews = await dbContext.Products
            .AsNoTracking()
            .CountAsync(
                product => product.Status == ProductStatus.PendingReview || product.Status == ProductStatus.NeedsAdminReview,
                cancellationToken);

        var newOrdersToday = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(
                order => order.CreatedAtUtc >= todayStartUtc && order.CreatedAtUtc < tomorrowStartUtc,
                cancellationToken);

        var openDisputes = await dbContext.Disputes
            .AsNoTracking()
            .CountAsync(
                dispute => dispute.Status == DisputeStatus.Open
                    || dispute.Status == DisputeStatus.AwaitingBuyer
                    || dispute.Status == DisputeStatus.AwaitingSeller
                    || dispute.Status == DisputeStatus.UnderAdminReview,
                cancellationToken);

        var pendingRefunds = await dbContext.Refunds
            .AsNoTracking()
            .CountAsync(
                refund => refund.Status == RefundStatus.Requested
                    || refund.Status == RefundStatus.Approved
                    || refund.Status == RefundStatus.Processing,
                cancellationToken);

        var pendingPayouts = await dbContext.SellerPayouts
            .AsNoTracking()
            .CountAsync(
                payout => payout.Status == SellerPayoutStatus.Pending || payout.Status == SellerPayoutStatus.OnHold,
                cancellationToken);

        return HttpResults.Ok(new AdminDashboardSummaryResponse(
            pendingSellerApprovals,
            pendingProductReviews,
            newOrdersToday,
            openDisputes,
            pendingRefunds,
            pendingPayouts,
            TotalGrossSalesPlaceholder: 0,
            PlatformCommissionPlaceholder: 0));
    }
}

public sealed record AdminDashboardSummaryResponse(
    int PendingSellerApprovals,
    int PendingProductReviews,
    int NewOrdersToday,
    int OpenDisputes,
    int PendingRefunds,
    int PendingPayouts,
    decimal TotalGrossSalesPlaceholder,
    decimal PlatformCommissionPlaceholder);
