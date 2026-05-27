using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Swyftly.Application.Notifications;
using Swyftly.Application.Sellers;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Returns;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Sellers;

public sealed class EfSellerScheduledReportService(
    SwyftlyDbContext dbContext,
    INotificationService notificationService,
    ILogger<EfSellerScheduledReportService> logger) : ISellerScheduledReportService
{
    private const string DefaultTimeZoneId = "Africa/Johannesburg";
    private const string DefaultSendTime = "08:00";

    public async Task<SellerReportScheduleResponse> GetOrCreateScheduleAsync(
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var schedule = await dbContext.SellerReportSchedules
            .SingleOrDefaultAsync(existing => existing.SellerId == sellerId, cancellationToken);

        if (schedule is null)
        {
            schedule = new SellerReportSchedule(sellerId, now);
            dbContext.SellerReportSchedules.Add(schedule);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(schedule);
    }

    public async Task<SellerReportScheduleSaveResult> SaveScheduleAsync(
        Guid sellerId,
        bool isVerifiedSeller,
        SellerReportScheduleRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (request.IsEnabled && !isVerifiedSeller)
        {
            return SellerReportScheduleSaveResult.Conflict(
                "SellerAnalytics.ScheduleRequiresVerification",
                "Scheduled analytics reports can be enabled only after seller verification is approved.");
        }

        if (!TryNormalize(request, out var normalized, out var errors))
        {
            return SellerReportScheduleSaveResult.Validation(errors);
        }

        var schedule = await dbContext.SellerReportSchedules
            .SingleOrDefaultAsync(existing => existing.SellerId == sellerId, cancellationToken);
        if (schedule is null)
        {
            schedule = new SellerReportSchedule(sellerId, now);
            dbContext.SellerReportSchedules.Add(schedule);
        }

        var nextRunAtUtc = normalized.IsEnabled
            ? ComputeNextRunUtc(normalized, now)
            : null;

        schedule.Update(
            normalized.IsEnabled,
            normalized.Frequency,
            normalized.ReportRange,
            normalized.SendDayOfWeek,
            normalized.SendDayOfMonth,
            normalized.SendTimeLocal,
            normalized.TimeZoneId,
            nextRunAtUtc,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return SellerReportScheduleSaveResult.Success(Map(schedule));
    }

    public async Task<SellerReportDigestSendResult> SendTestDigestAsync(
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var seller = await GetSellerProjectionAsync(sellerId, cancellationToken);
        if (seller is null)
        {
            return new SellerReportDigestSendResult(false, null, "Seller profile was not found.");
        }

        var schedule = await dbContext.SellerReportSchedules
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.SellerId == sellerId, cancellationToken);
        var reportRange = schedule?.ReportRange ?? SellerReportRange.Last30Days;
        var timeZoneId = schedule?.TimeZoneId ?? DefaultTimeZoneId;
        var period = ResolveReportPeriod(reportRange, now, timeZoneId);
        var digest = await BuildDigestAsync(sellerId, period.PeriodStartUtc, period.PeriodEndUtc, cancellationToken);
        var notification = await CreateDigestNotificationAsync(
            seller.UserId,
            "Seller analytics test digest",
            BuildDigestMessage(digest, isTest: true),
            now,
            cancellationToken);

        return new SellerReportDigestSendResult(true, notification?.NotificationId);
    }

    public async Task<SellerScheduledReportProcessingResult> ProcessDueReportsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var schedules = await dbContext.SellerReportSchedules
            .Where(schedule => schedule.IsEnabled
                && schedule.NextRunAtUtc != null
                && schedule.NextRunAtUtc <= now)
            .OrderBy(schedule => schedule.NextRunAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processed = 0;
        var sent = 0;
        var failed = 0;
        var skippedDuplicate = 0;

        foreach (var schedule in schedules)
        {
            processed++;
            var dueAtUtc = schedule.NextRunAtUtc ?? now;
            var nextRunAtUtc = ComputeNextRunUtc(schedule, dueAtUtc.AddMinutes(1));

            try
            {
                var seller = await GetSellerProjectionAsync(schedule.SellerId, cancellationToken);
                if (seller is null || seller.VerificationStatus != SellerVerificationStatus.Verified)
                {
                    failed++;
                    schedule.MarkFailed("Seller is no longer verified for scheduled analytics reports.", now, nextRunAtUtc);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var period = ResolveReportPeriod(schedule.ReportRange, dueAtUtc, schedule.TimeZoneId);
                var existingRun = await dbContext.SellerReportScheduleRuns.AnyAsync(
                    run => run.SellerReportScheduleId == schedule.Id
                        && run.ReportPeriodStartUtc == period.PeriodStartUtc
                        && run.ReportPeriodEndUtc == period.PeriodEndUtc,
                    cancellationToken);
                if (existingRun)
                {
                    skippedDuplicate++;
                    schedule.MarkSent(period.PeriodStartUtc, period.PeriodEndUtc, now, nextRunAtUtc);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var run = new SellerReportScheduleRun(
                    schedule.Id,
                    schedule.SellerId,
                    period.PeriodStartUtc,
                    period.PeriodEndUtc,
                    now);
                dbContext.SellerReportScheduleRuns.Add(run);

                var digest = await BuildDigestAsync(schedule.SellerId, period.PeriodStartUtc, period.PeriodEndUtc, cancellationToken);
                var notification = await CreateDigestNotificationAsync(
                    seller.UserId,
                    "Seller analytics digest",
                    BuildDigestMessage(digest, isTest: false),
                    now,
                    cancellationToken);

                run.MarkSent(notification?.NotificationId, now);
                schedule.MarkSent(period.PeriodStartUtc, period.PeriodEndUtc, now, nextRunAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(
                    exception,
                    "Scheduled seller analytics report failed for schedule {ScheduleId}.",
                    schedule.Id);
                schedule.MarkFailed(exception.Message, now, nextRunAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new SellerScheduledReportProcessingResult(processed, sent, failed, skippedDuplicate);
    }

    private async Task<NotificationResult?> CreateDigestNotificationAsync(
        Guid sellerUserId,
        string title,
        string message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await notificationService.CreateAsync(
            new CreateNotificationRequest(
                sellerUserId,
                SellerNotificationTypes.SellerAnalyticsDigestReady,
                title,
                message,
                "SellerAnalytics",
                null,
                now),
            cancellationToken);
    }

    private async Task<SellerProjection?> GetSellerProjectionAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        return await dbContext.SellerProfiles
            .AsNoTracking()
            .Where(seller => seller.Id == sellerId)
            .Select(seller => new SellerProjection(seller.Id, seller.UserId, seller.VerificationStatus))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<SellerAnalyticsDigest> BuildDigestAsync(
        Guid sellerId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.SellerId == sellerId
                && order.CreatedAtUtc >= fromUtc
                && order.CreatedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var salesOrders = orders.Where(order => IsSalesOrderStatus(order.Status)).ToArray();
        var refunds = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.SellerId == sellerId
                && refund.Status == RefundStatus.Refunded
                && refund.RefundedAtUtc >= fromUtc
                && refund.RefundedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);
        var returnCount = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(returnRequest => returnRequest.SellerId == sellerId
                && returnRequest.RequestedAtUtc >= fromUtc
                && returnRequest.RequestedAtUtc <= toUtc,
                cancellationToken);
        var productIds = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId)
            .Select(product => product.Id)
            .ToArrayAsync(cancellationToken);
        var inventoryAlerts = productIds.Length == 0
            ? 0
            : await dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId)
                    && variant.StockQuantity - variant.ReservedQuantity <= 5)
                .CountAsync(cancellationToken);
        var campaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == sellerId)
            .Select(campaign => campaign.Id)
            .ToArrayAsync(cancellationToken);
        var adSpend = campaignIds.Length == 0
            ? 0m
            : await dbContext.AdCharges
                .AsNoTracking()
                .Where(charge => campaignIds.Contains(charge.AdCampaignId)
                    && charge.ChargedAtUtc >= fromUtc
                    && charge.ChargedAtUtc <= toUtc)
                .SumAsync(charge => (decimal?)charge.Amount, cancellationToken) ?? 0m;
        var attributedRevenue = campaignIds.Length == 0
            ? 0m
            : await dbContext.AdConversions
                .AsNoTracking()
                .Where(conversion => campaignIds.Contains(conversion.AdCampaignId)
                    && conversion.OccurredAtUtc >= fromUtc
                    && conversion.OccurredAtUtc <= toUtc)
                .SumAsync(conversion => (decimal?)conversion.RevenueAmount, cancellationToken) ?? 0m;

        var grossSales = salesOrders.Sum(order => order.TotalAmount);
        var refundedAmount = refunds.Sum(refund => refund.Amount);

        return new SellerAnalyticsDigest(
            fromUtc,
            toUtc,
            salesOrders.Length,
            grossSales,
            refundedAmount,
            grossSales - refundedAmount,
            salesOrders.SelectMany(order => order.Items).Sum(item => item.Quantity),
            returnCount,
            inventoryAlerts,
            adSpend,
            attributedRevenue);
    }

    private static string BuildDigestMessage(SellerAnalyticsDigest digest, bool isTest)
    {
        var prefix = isTest ? "Test digest generated. " : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}Analytics from {digest.FromUtc:yyyy-MM-dd} to {digest.ToUtc:yyyy-MM-dd}: {digest.OrderCount} orders, {digest.UnitsSold} units sold, gross sales ZAR {digest.GrossSales:0.00}, refunds ZAR {digest.RefundedAmount:0.00}, net sales ZAR {digest.NetSales:0.00}, {digest.ReturnCount} returns, {digest.InventoryAlertCount} low/out-of-stock variants, ad spend ZAR {digest.AdSpend:0.00}, attributed ad revenue ZAR {digest.AttributedAdRevenue:0.00}. Open Seller analytics for full tables and CSV exports.");
    }

    private static bool TryNormalize(
        SellerReportScheduleRequest request,
        out NormalizedScheduleRequest normalized,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (!Enum.TryParse(request.Frequency, ignoreCase: true, out SellerReportFrequency frequency)
            || !Enum.IsDefined(frequency))
        {
            validationErrors["frequency"] = ["Frequency must be Weekly or Monthly."];
        }

        if (!Enum.TryParse(request.ReportRange, ignoreCase: true, out SellerReportRange reportRange)
            || !Enum.IsDefined(reportRange))
        {
            validationErrors["reportRange"] = ["Report range must be Last7Days, Last30Days, or MonthToDate."];
        }

        DayOfWeek? sendDayOfWeek = null;
        if (frequency == SellerReportFrequency.Weekly)
        {
            if (string.IsNullOrWhiteSpace(request.SendDayOfWeek)
                || !Enum.TryParse(request.SendDayOfWeek, ignoreCase: true, out DayOfWeek parsedDay))
            {
                validationErrors["sendDayOfWeek"] = ["Send day of week is required for weekly reports."];
            }
            else
            {
                sendDayOfWeek = parsedDay;
            }
        }

        int? sendDayOfMonth = null;
        if (frequency == SellerReportFrequency.Monthly)
        {
            if (request.SendDayOfMonth is < 1 or > 28)
            {
                validationErrors["sendDayOfMonth"] = ["Send day of month must be between 1 and 28."];
            }
            else
            {
                sendDayOfMonth = request.SendDayOfMonth;
            }
        }

        var sendTime = request.SendTimeLocal?.Trim() ?? string.Empty;
        if (!TimeOnly.TryParseExact(sendTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            validationErrors["sendTimeLocal"] = ["Send time must use HH:mm 24-hour format."];
        }

        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? DefaultTimeZoneId
            : request.TimeZoneId.Trim();
        if (!TryResolveTimeZone(timeZoneId, out _))
        {
            validationErrors["timeZoneId"] = ["Time zone id is not supported by this server."];
        }

        normalized = new NormalizedScheduleRequest(
            request.IsEnabled,
            frequency,
            reportRange,
            sendDayOfWeek,
            sendDayOfMonth,
            sendTime.Length == 0 ? DefaultSendTime : sendTime,
            timeZoneId);
        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    private static DateTimeOffset? ComputeNextRunUtc(NormalizedScheduleRequest request, DateTimeOffset now)
    {
        if (!request.IsEnabled || !TryResolveTimeZone(request.TimeZoneId, out var timeZone))
        {
            return null;
        }

        return ComputeNextRunUtc(
            request.Frequency,
            request.SendDayOfWeek,
            request.SendDayOfMonth,
            request.SendTimeLocal,
            timeZone,
            now);
    }

    private static DateTimeOffset? ComputeNextRunUtc(SellerReportSchedule schedule, DateTimeOffset now)
    {
        if (!schedule.IsEnabled || !TryResolveTimeZone(schedule.TimeZoneId, out var timeZone))
        {
            return null;
        }

        return ComputeNextRunUtc(
            schedule.Frequency,
            schedule.SendDayOfWeek,
            schedule.SendDayOfMonth,
            schedule.SendTimeLocal,
            timeZone,
            now);
    }

    private static DateTimeOffset ComputeNextRunUtc(
        SellerReportFrequency frequency,
        DayOfWeek? sendDayOfWeek,
        int? sendDayOfMonth,
        string sendTimeLocal,
        TimeZoneInfo timeZone,
        DateTimeOffset now)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        _ = TimeOnly.TryParseExact(sendTimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sendTime);
        DateTime candidateLocal;

        if (frequency == SellerReportFrequency.Weekly)
        {
            var targetDay = sendDayOfWeek ?? DayOfWeek.Monday;
            var daysUntil = ((int)targetDay - (int)localNow.DayOfWeek + 7) % 7;
            candidateLocal = localNow.Date.AddDays(daysUntil).Add(sendTime.ToTimeSpan());
            if (candidateLocal <= localNow.DateTime)
            {
                candidateLocal = candidateLocal.AddDays(7);
            }
        }
        else
        {
            var targetDay = Math.Clamp(sendDayOfMonth ?? 1, 1, 28);
            candidateLocal = new DateTime(localNow.Year, localNow.Month, targetDay).Add(sendTime.ToTimeSpan());
            if (candidateLocal <= localNow.DateTime)
            {
                var nextMonth = localNow.Date.AddMonths(1);
                candidateLocal = new DateTime(nextMonth.Year, nextMonth.Month, targetDay).Add(sendTime.ToTimeSpan());
            }
        }

        return new DateTimeOffset(candidateLocal, timeZone.GetUtcOffset(candidateLocal)).ToUniversalTime();
    }

    private static ReportPeriod ResolveReportPeriod(
        SellerReportRange range,
        DateTimeOffset periodEndUtc,
        string timeZoneId)
    {
        if (range == SellerReportRange.MonthToDate && TryResolveTimeZone(timeZoneId, out var timeZone))
        {
            var localEnd = TimeZoneInfo.ConvertTime(periodEndUtc, timeZone);
            var localStart = new DateTime(localEnd.Year, localEnd.Month, 1);
            var startUtc = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart)).ToUniversalTime();
            return new ReportPeriod(startUtc, periodEndUtc.ToUniversalTime());
        }

        var start = range switch
        {
            SellerReportRange.Last7Days => periodEndUtc.AddDays(-7),
            SellerReportRange.Last30Days => periodEndUtc.AddDays(-30),
            SellerReportRange.MonthToDate => new DateTimeOffset(periodEndUtc.Year, periodEndUtc.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => periodEndUtc.AddDays(-30)
        };

        return new ReportPeriod(start.ToUniversalTime(), periodEndUtc.ToUniversalTime());
    }

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            if (string.Equals(timeZoneId, DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
                    return true;
                }
                catch (TimeZoneNotFoundException)
                {
                }
            }
        }
        catch (InvalidTimeZoneException)
        {
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
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

    private static SellerReportScheduleResponse Map(SellerReportSchedule schedule) =>
        new(
            schedule.Id,
            schedule.IsEnabled,
            schedule.Frequency.ToString(),
            schedule.ReportRange.ToString(),
            schedule.SendDayOfWeek?.ToString(),
            schedule.SendDayOfMonth,
            schedule.SendTimeLocal,
            schedule.TimeZoneId,
            schedule.NextRunAtUtc,
            schedule.LastSentAtUtc,
            schedule.LastReportPeriodStartUtc,
            schedule.LastReportPeriodEndUtc,
            schedule.LastFailureReason,
            schedule.LastFailedAtUtc);

    private sealed record NormalizedScheduleRequest(
        bool IsEnabled,
        SellerReportFrequency Frequency,
        SellerReportRange ReportRange,
        DayOfWeek? SendDayOfWeek,
        int? SendDayOfMonth,
        string SendTimeLocal,
        string TimeZoneId);

    private sealed record SellerProjection(
        Guid SellerId,
        Guid UserId,
        SellerVerificationStatus VerificationStatus);

    private sealed record ReportPeriod(
        DateTimeOffset PeriodStartUtc,
        DateTimeOffset PeriodEndUtc);

    private sealed record SellerAnalyticsDigest(
        DateTimeOffset FromUtc,
        DateTimeOffset ToUtc,
        int OrderCount,
        decimal GrossSales,
        decimal RefundedAmount,
        decimal NetSales,
        int UnitsSold,
        int ReturnCount,
        int InventoryAlertCount,
        decimal AdSpend,
        decimal AttributedAdRevenue);
}
