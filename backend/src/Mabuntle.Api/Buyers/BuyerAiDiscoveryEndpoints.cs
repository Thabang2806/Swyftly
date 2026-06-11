using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Buyers;

public static class BuyerAiDiscoveryEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapBuyerAiDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/buyer/ai-discovery")
            .WithTags("Buyer AI Discovery")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly);

        group.MapGet("/preferences", GetPreferencesAsync)
            .WithName("GetBuyerAiDiscoveryPreferences")
            .WithSummary("Returns buyer AI discovery history preferences.")
            .Produces<BuyerAiDiscoveryPreferenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/preferences", UpdatePreferencesAsync)
            .WithName("UpdateBuyerAiDiscoveryPreferences")
            .WithSummary("Updates buyer AI discovery history preferences.")
            .Produces<BuyerAiDiscoveryPreferenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/history", GetHistoryAsync)
            .WithName("GetBuyerAiDiscoveryHistory")
            .WithSummary("Returns buyer-owned AI discovery history.")
            .Produces<BuyerAiDiscoveryHistoryListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/history", DeleteAllHistoryAsync)
            .WithName("DeleteBuyerAiDiscoveryHistory")
            .WithSummary("Deletes all buyer-owned AI discovery history.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/history/{historyId:guid}", DeleteHistoryItemAsync)
            .WithName("DeleteBuyerAiDiscoveryHistoryItem")
            .WithSummary("Deletes one buyer-owned AI discovery history item.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPreferencesAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var preference = await dbContext.BuyerAiDiscoveryPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.BuyerId == buyer.Id, cancellationToken);

        return HttpResults.Ok(new BuyerAiDiscoveryPreferenceResponse(
            preference?.HistoryEnabled ?? false,
            preference?.PersonalizationEnabled ?? false,
            preference?.UpdatedAtUtc));
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        BuyerAiDiscoveryPreferenceRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var now = timeProvider.GetUtcNow();
        var preference = await dbContext.BuyerAiDiscoveryPreferences
            .SingleOrDefaultAsync(item => item.BuyerId == buyer.Id, cancellationToken);

        if (preference is null)
        {
            preference = new BuyerAiDiscoveryPreference(
                buyer.Id,
                request.HistoryEnabled,
                now,
                request.PersonalizationEnabled ?? false);
            dbContext.BuyerAiDiscoveryPreferences.Add(preference);
        }
        else
        {
            preference.SetPreferences(
                request.HistoryEnabled,
                request.PersonalizationEnabled ?? preference.PersonalizationEnabled,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(new BuyerAiDiscoveryPreferenceResponse(
            preference.HistoryEnabled,
            preference.PersonalizationEnabled,
            preference.UpdatedAtUtc));
    }

    private static async Task<IResult> GetHistoryAsync(
        int? page,
        int? pageSize,
        string? tool,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var resolvedPage = Math.Max(page ?? DefaultPage, 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        BuyerGrowthSourceTool? sourceTool = null;
        if (!string.IsNullOrWhiteSpace(tool)
            && (!Enum.TryParse(tool, ignoreCase: true, out BuyerGrowthSourceTool parsedTool) || !Enum.IsDefined(parsedTool)))
        {
            return HttpResults.Problem(
                title: "BuyerAiDiscovery.InvalidTool",
                detail: "tool must be Assistant or VisualSearch.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        else if (!string.IsNullOrWhiteSpace(tool))
        {
            sourceTool = Enum.Parse<BuyerGrowthSourceTool>(tool, ignoreCase: true);
        }

        var query = dbContext.BuyerAiDiscoveryHistory
            .AsNoTracking()
            .Where(item => item.BuyerId == buyer.Id);

        if (sourceTool.HasValue)
        {
            query = query.Where(item => item.SourceTool == sourceTool.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var history = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(cancellationToken);

        var productIds = history.SelectMany(item => item.ProductIds).Distinct().ToArray();
        var products = productIds.Length == 0
            ? new Dictionary<Guid, BuyerAiDiscoveryHistoryProductResponse>()
            : await dbContext.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .Select(product => new BuyerAiDiscoveryHistoryProductResponse(
                    product.Id,
                    product.Title ?? "Untitled product",
                    product.Slug ?? product.Id.ToString("N")))
                .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        return HttpResults.Ok(new BuyerAiDiscoveryHistoryListResponse(
            history.Select(item => MapHistory(item, products)).ToArray(),
            totalCount,
            resolvedPage,
            resolvedPageSize));
    }

    private static async Task<IResult> DeleteAllHistoryAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var history = await dbContext.BuyerAiDiscoveryHistory
            .Where(item => item.BuyerId == buyer.Id)
            .ToListAsync(cancellationToken);
        dbContext.BuyerAiDiscoveryHistory.RemoveRange(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.NoContent();
    }

    private static async Task<IResult> DeleteHistoryItemAsync(
        Guid historyId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var history = await dbContext.BuyerAiDiscoveryHistory
            .SingleOrDefaultAsync(item => item.Id == historyId && item.BuyerId == buyer.Id, cancellationToken);
        if (history is null)
        {
            return HistoryNotFound();
        }

        dbContext.BuyerAiDiscoveryHistory.Remove(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.NoContent();
    }

    private static BuyerAiDiscoveryHistoryResponse MapHistory(
        BuyerAiDiscoveryHistory history,
        IReadOnlyDictionary<Guid, BuyerAiDiscoveryHistoryProductResponse> products) =>
        new(
            history.Id,
            history.SourceTool.ToString(),
            history.Category,
            history.Colour,
            history.Material,
            history.ConfidenceBand?.ToString(),
            history.ResultCount,
            history.ProductIds,
            history.ProductIds.Where(products.ContainsKey).Select(productId => products[productId]).ToArray(),
            history.SourceRoute,
            history.CreatedAtUtc);

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Buyer.NotFound",
            detail: "Buyer profile was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult HistoryNotFound() =>
        HttpResults.Problem(
            title: "BuyerAiDiscovery.HistoryNotFound",
            detail: "AI discovery history item was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record BuyerAiDiscoveryPreferenceRequest(
    bool HistoryEnabled,
    bool? PersonalizationEnabled = null);

public sealed record BuyerAiDiscoveryPreferenceResponse(
    bool HistoryEnabled,
    bool PersonalizationEnabled,
    DateTimeOffset? UpdatedAtUtc);

public sealed record BuyerAiDiscoveryHistoryListResponse(
    IReadOnlyCollection<BuyerAiDiscoveryHistoryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record BuyerAiDiscoveryHistoryResponse(
    Guid HistoryId,
    string SourceTool,
    string? Category,
    string? Colour,
    string? Material,
    string? ConfidenceBand,
    int ResultCount,
    IReadOnlyCollection<Guid> ProductIds,
    IReadOnlyCollection<BuyerAiDiscoveryHistoryProductResponse> Products,
    string? SourceRoute,
    DateTimeOffset CreatedAtUtc);

public sealed record BuyerAiDiscoveryHistoryProductResponse(
    Guid ProductId,
    string Title,
    string Slug);
