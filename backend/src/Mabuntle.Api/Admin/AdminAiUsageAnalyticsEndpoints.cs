using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Ai;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminAiUsageAnalyticsEndpoints
{
    private const string ListingAssistantFeature = "ListingAssistant";
    private const string ProductModerationFeature = "ProductModeration";

    public static IEndpointRouteBuilder MapAdminAiUsageAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/analytics/ai-usage", GetAiUsageAnalyticsAsync)
            .WithTags("Admin Analytics")
            .WithName("GetAdminAiUsageAnalytics")
            .WithSummary("Returns aggregate AI usage, cost, quality, moderation, and failure analytics for admins.")
            .RequireAuthorization(MabuntlePolicies.AdminOnly)
            .Produces<AdminAiUsageAnalyticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetAiUsageAnalyticsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? featureName,
        Guid? sellerId,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (sellerId == Guid.Empty)
        {
            return InvalidSellerProblem();
        }

        var range = ResolveRange(fromUtc, toUtc, timeProvider);
        if (!range.IsValid)
        {
            return InvalidRangeProblem();
        }

        var normalizedFeatureName = NormalizeFilter(featureName);
        var usageLogs = await dbContext.AiUsageLogs
            .AsNoTracking()
            .Where(log => log.CreatedAtUtc >= range.FromUtc && log.CreatedAtUtc <= range.ToUtc)
            .Where(log => sellerId == null || log.SellerId == sellerId)
            .Where(log => normalizedFeatureName == null || log.FeatureName == normalizedFeatureName)
            .ToListAsync(cancellationToken);

        var includeListingSuggestions = IncludeFeature(normalizedFeatureName, ListingAssistantFeature);
        var suggestions = includeListingSuggestions
            ? await dbContext.AiProductSuggestions
                .AsNoTracking()
                .Where(suggestion => suggestion.CreatedAtUtc >= range.FromUtc && suggestion.CreatedAtUtc <= range.ToUtc)
                .Where(suggestion => sellerId == null || suggestion.SellerId == sellerId)
                .ToListAsync(cancellationToken)
            : [];

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

        var includeModeration = IncludeFeature(normalizedFeatureName, ProductModerationFeature);
        var moderationResults = includeModeration
            ? await dbContext.AiModerationResults
                .AsNoTracking()
                .Where(result => result.CreatedAtUtc >= range.FromUtc && result.CreatedAtUtc <= range.ToUtc)
                .Where(result => sellerId == null || result.SellerId == sellerId)
                .ToListAsync(cancellationToken)
            : [];

        var topSellerUsage = await BuildTopSellerUsageAsync(usageLogs, dbContext, cancellationToken);
        var generatedSuggestions = suggestions.Count;
        var acceptedSuggestions = suggestions.Count(IsAcceptedOrApplied);

        return HttpResults.Ok(new AdminAiUsageAnalyticsResponse(
            range.FromUtc,
            range.ToUtc,
            timeProvider.GetUtcNow(),
            normalizedFeatureName,
            sellerId,
            new AdminAiUsageTotalsResponse(
                usageLogs.Count,
                usageLogs.Count(log => log.Success),
                usageLogs.Count(log => !log.Success),
                usageLogs.Count == 0 ? 0 : decimal.Round((decimal)usageLogs.Count(log => !log.Success) / usageLogs.Count, 4),
                usageLogs.Sum(log => log.InputTokenEstimate ?? 0),
                usageLogs.Sum(log => log.OutputTokenEstimate ?? 0),
                usageLogs.Sum(log => log.CostEstimate ?? 0),
                usageLogs.Count == 0 ? 0 : decimal.Round((decimal)usageLogs.Average(log => log.LatencyMs), 2)),
            new AdminAiSuggestionAnalyticsResponse(
                generatedSuggestions,
                acceptedSuggestions,
                generatedSuggestions == 0 ? 0 : decimal.Round((decimal)acceptedSuggestions / generatedSuggestions, 4),
                suggestions.Count(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied),
                suggestions.Select(suggestion => suggestion.ProductId).Distinct().Count(),
                suggestions
                    .Where(suggestion => suggestion.Status == AiProductSuggestionStatus.Applied)
                    .Select(suggestion => suggestion.ProductId)
                    .Distinct()
                    .Count(),
                suggestions.Count == 0 ? 0 : decimal.Round(suggestions.Average(suggestion => suggestion.QualityScore), 2),
                null,
                "Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.",
                fieldAudits.Count,
                fieldAudits.Count(audit => audit.WasAccepted),
                fieldAudits.Count(audit => audit.WasEdited)),
            new AdminAiModerationAnalyticsResponse(
                moderationResults.Count,
                moderationResults.Count(result => result.NeedsAdminReview),
                moderationResults.Count(result => result.RiskLevel == AiModerationRiskLevel.Low),
                moderationResults.Count(result => result.RiskLevel == AiModerationRiskLevel.Medium),
                moderationResults.Count(result => result.RiskLevel == AiModerationRiskLevel.High)),
            usageLogs
                .GroupBy(log => log.FeatureName)
                .Select(group => new AdminAiFeatureUsageResponse(
                    group.Key,
                    group.Count(),
                    group.Count(log => log.Success),
                    group.Count(log => !log.Success),
                    group.Sum(log => log.CostEstimate ?? 0),
                    decimal.Round((decimal)group.Average(log => log.LatencyMs), 2)))
                .OrderByDescending(feature => feature.Requests)
                .ThenBy(feature => feature.FeatureName)
                .ToArray(),
            usageLogs
                .GroupBy(log => log.ModelUsed)
                .Select(group => new AdminAiModelUsageResponse(
                    group.Key,
                    group.Count(),
                    group.Sum(log => log.InputTokenEstimate ?? 0),
                    group.Sum(log => log.OutputTokenEstimate ?? 0),
                    group.Sum(log => log.CostEstimate ?? 0),
                    decimal.Round((decimal)group.Average(log => log.LatencyMs), 2)))
                .OrderByDescending(model => model.Requests)
                .ThenBy(model => model.ModelUsed)
                .ToArray(),
            topSellerUsage));
    }

    private static async Task<IReadOnlyCollection<AdminAiTopSellerUsageResponse>> BuildTopSellerUsageAsync(
        IReadOnlyCollection<AiUsageLog> usageLogs,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sellerIds = usageLogs
            .Where(log => log.SellerId.HasValue)
            .Select(log => log.SellerId!.Value)
            .Distinct()
            .ToArray();
        var sellerNames = await dbContext.SellerProfiles
            .AsNoTracking()
            .Where(seller => sellerIds.Contains(seller.Id))
            .Select(seller => new { seller.Id, seller.DisplayName })
            .ToDictionaryAsync(seller => seller.Id, seller => seller.DisplayName, cancellationToken);

        return usageLogs
            .Where(log => log.SellerId.HasValue)
            .GroupBy(log => log.SellerId!.Value)
            .Select(group => new AdminAiTopSellerUsageResponse(
                group.Key,
                sellerNames.GetValueOrDefault(group.Key),
                group.Count(),
                group.Count(log => !log.Success),
                group.Sum(log => log.CostEstimate ?? 0),
                decimal.Round((decimal)group.Average(log => log.LatencyMs), 2)))
            .OrderByDescending(seller => seller.Requests)
            .ThenByDescending(seller => seller.EstimatedCost)
            .Take(10)
            .ToArray();
    }

    private static bool IncludeFeature(string? requestedFeatureName, string featureName) =>
        requestedFeatureName is null || string.Equals(requestedFeatureName, featureName, StringComparison.OrdinalIgnoreCase);

    private static bool IsAcceptedOrApplied(AiProductSuggestion suggestion) =>
        suggestion.Status is AiProductSuggestionStatus.Accepted or AiProductSuggestionStatus.Applied;

    private static string? NormalizeFilter(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static ReportDateRange ResolveRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, TimeProvider timeProvider)
    {
        var resolvedTo = (toUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var resolvedFrom = (fromUtc ?? resolvedTo.AddDays(-30)).ToUniversalTime();

        return new ReportDateRange(resolvedFrom, resolvedTo, resolvedFrom <= resolvedTo);
    }

    private static IResult InvalidRangeProblem() =>
        HttpResults.Problem(
            title: "Analytics.InvalidDateRange",
            detail: "fromUtc must be earlier than or equal to toUtc.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult InvalidSellerProblem() =>
        HttpResults.Problem(
            title: "Analytics.InvalidSellerId",
            detail: "sellerId must be a non-empty GUID when provided.",
            statusCode: StatusCodes.Status400BadRequest);

    private sealed record ReportDateRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc, bool IsValid);
}

public sealed record AdminAiUsageAnalyticsResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset GeneratedAtUtc,
    string? FeatureName,
    Guid? SellerId,
    AdminAiUsageTotalsResponse Totals,
    AdminAiSuggestionAnalyticsResponse Suggestions,
    AdminAiModerationAnalyticsResponse Moderation,
    IReadOnlyCollection<AdminAiFeatureUsageResponse> FeatureUsage,
    IReadOnlyCollection<AdminAiModelUsageResponse> ModelUsage,
    IReadOnlyCollection<AdminAiTopSellerUsageResponse> TopSellers);

public sealed record AdminAiUsageTotalsResponse(
    int Requests,
    int SuccessfulRequests,
    int FailedRequests,
    decimal FailureRate,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    decimal AverageLatencyMs);

public sealed record AdminAiSuggestionAnalyticsResponse(
    int ProductSuggestionsGenerated,
    int ProductSuggestionsAccepted,
    decimal SuggestionAcceptanceRate,
    int ProductSuggestionsApplied,
    int ProductsTouchedByAi,
    int ProductsImprovedWithAi,
    decimal AverageListingQualityScore,
    decimal? AverageQualityScoreImprovement,
    string QualityScoreImprovementNote,
    int FieldAuditCount,
    int FieldValuesAccepted,
    int FieldValuesEdited);

public sealed record AdminAiModerationAnalyticsResponse(
    int ModerationChecks,
    int AdminReviewFlags,
    int LowRiskFlags,
    int MediumRiskFlags,
    int HighRiskFlags);

public sealed record AdminAiFeatureUsageResponse(
    string FeatureName,
    int Requests,
    int SuccessfulRequests,
    int FailedRequests,
    decimal EstimatedCost,
    decimal AverageLatencyMs);

public sealed record AdminAiModelUsageResponse(
    string ModelUsed,
    int Requests,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    decimal AverageLatencyMs);

public sealed record AdminAiTopSellerUsageResponse(
    Guid SellerId,
    string? SellerDisplayName,
    int Requests,
    int FailedRequests,
    decimal EstimatedCost,
    decimal AverageLatencyMs);
