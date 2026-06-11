using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Advertising;
using Mabuntle.Application.Identity;
using Mabuntle.Api.Sellers;
using Mabuntle.Domain.Advertising;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Advertising;

public static class SellerAdCampaignEndpoints
{
    public static IEndpointRouteBuilder MapSellerAdCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/ad-campaigns")
            .WithTags("Seller Ad Campaigns")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapPost("", CreateCampaignAsync)
            .WithName("CreateSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", ListCampaignsAsync)
            .WithName("ListSellerAdCampaigns")
            .Produces<IReadOnlyCollection<SellerAdCampaignResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetCampaignAsync)
            .WithName("GetSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateCampaignAsync)
            .WithName("UpdateSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/submit-review", SubmitReviewAsync)
            .WithName("SubmitSellerAdCampaignForReview")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/pause", PauseCampaignAsync)
            .WithName("PauseSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/resume", ResumeCampaignAsync)
            .WithName("ResumeSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", CancelCampaignAsync)
            .WithName("CancelSellerAdCampaign")
            .Produces<SellerAdCampaignResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateCampaignAsync(
        UpsertSellerAdCampaignRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryParseCampaignType(request.CampaignType, out var campaignType))
        {
            return InvalidCampaignType();
        }

        var eligibility = await eligibilityService.ValidateAsync(seller.Id, request.ProductIds ?? [], cancellationToken);
        if (!eligibility.IsEligible)
        {
            return EligibilityValidation(eligibility);
        }

        var now = timeProvider.GetUtcNow();
        AdCampaign campaign;
        AdBudget budget;
        try
        {
            campaign = new AdCampaign(
                seller.Id,
                request.Name,
                campaignType,
                request.StartsAtUtc,
                request.EndsAtUtc,
                now);
            campaign.ReplaceProducts(request.ProductIds ?? [], now);
            budget = new AdBudget(
                campaign.Id,
                request.Budget.Currency,
                request.Budget.DailyBudget,
                request.Budget.TotalBudget,
                request.Budget.MaxCostPerClick,
                now);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Validation("campaign", exception.Message);
        }

        dbContext.AdCampaigns.Add(campaign);
        dbContext.AdBudgets.Add(budget);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created(
            $"/api/seller/ad-campaigns/{campaign.Id}",
            await CreateCampaignResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<IResult> ListCampaignsAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var campaignIds = await dbContext.AdCampaigns
            .AsNoTracking()
            .Where(campaign => campaign.SellerId == seller.Id)
            .OrderByDescending(campaign => campaign.UpdatedAtUtc)
            .Select(campaign => campaign.Id)
            .ToListAsync(cancellationToken);

        var responses = new List<SellerAdCampaignResponse>();
        foreach (var campaignId in campaignIds)
        {
            responses.Add(await CreateCampaignResponseAsync(campaignId, dbContext, eligibilityService, cancellationToken));
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetCampaignAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        CancellationToken cancellationToken)
    {
        var campaign = await GetOwnedCampaignAsync(id, principal, dbContext, cancellationToken);
        return campaign is null
            ? CampaignNotFound()
            : HttpResults.Ok(await CreateCampaignResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<IResult> UpdateCampaignAsync(
        Guid id,
        UpsertSellerAdCampaignRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var campaign = await GetOwnedCampaignForUpdateAsync(id, principal, dbContext, cancellationToken);
        if (campaign is null)
        {
            return CampaignNotFound();
        }

        if (!TryParseCampaignType(request.CampaignType, out var campaignType))
        {
            return InvalidCampaignType();
        }

        var eligibility = await eligibilityService.ValidateAsync(campaign.SellerId, request.ProductIds ?? [], cancellationToken);
        if (!eligibility.IsEligible)
        {
            return EligibilityValidation(eligibility);
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            campaign.UpdateDraft(request.Name, campaignType, request.StartsAtUtc, request.EndsAtUtc, now);
            var existingProducts = campaign.Products.ToArray();
            dbContext.AdCampaignProducts.RemoveRange(existingProducts);
            campaign.ReplaceProducts(request.ProductIds ?? [], now);
            dbContext.AdCampaignProducts.AddRange(campaign.Products);

            var budget = await dbContext.AdBudgets.SingleOrDefaultAsync(item => item.AdCampaignId == campaign.Id, cancellationToken);
            if (budget is null)
            {
                dbContext.AdBudgets.Add(new AdBudget(
                    campaign.Id,
                    request.Budget.Currency,
                    request.Budget.DailyBudget,
                    request.Budget.TotalBudget,
                    request.Budget.MaxCostPerClick,
                    now));
            }
            else
            {
                budget.Update(
                    request.Budget.Currency,
                    request.Budget.DailyBudget,
                    request.Budget.TotalBudget,
                    request.Budget.MaxCostPerClick,
                    now);
            }
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("campaign", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCampaignResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<IResult> SubmitReviewAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var campaign = await GetOwnedCampaignForUpdateAsync(id, principal, dbContext, cancellationToken);
        if (campaign is null)
        {
            return CampaignNotFound();
        }

        var budgetExists = await dbContext.AdBudgets.AnyAsync(budget => budget.AdCampaignId == campaign.Id, cancellationToken);
        if (!budgetExists)
        {
            return Validation("budget", "Campaign must have a budget before review.");
        }

        var productIds = campaign.Products.Select(product => product.ProductId).ToArray();
        var eligibility = await eligibilityService.ValidateAsync(campaign.SellerId, productIds, cancellationToken);
        if (!eligibility.IsEligible)
        {
            return EligibilityValidation(eligibility);
        }

        try
        {
            campaign.SubmitForReview(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCampaignResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<IResult> PauseCampaignAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangeCampaignStateAsync(
            id,
            principal,
            dbContext,
            eligibilityService,
            campaign => campaign.Pause(timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static async Task<IResult> ResumeCampaignAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangeCampaignStateAsync(
            id,
            principal,
            dbContext,
            eligibilityService,
            campaign => campaign.Resume(timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static async Task<IResult> CancelCampaignAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangeCampaignStateAsync(
            id,
            principal,
            dbContext,
            eligibilityService,
            campaign => campaign.Cancel(timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static async Task<IResult> ChangeCampaignStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        Action<AdCampaign> change,
        CancellationToken cancellationToken)
    {
        var campaign = await GetOwnedCampaignForUpdateAsync(id, principal, dbContext, cancellationToken);
        if (campaign is null)
        {
            return CampaignNotFound();
        }

        try
        {
            change(campaign);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(await CreateCampaignResponseAsync(campaign.Id, dbContext, eligibilityService, cancellationToken));
    }

    private static async Task<SellerAdCampaignResponse> CreateCampaignResponseAsync(
        Guid campaignId,
        MabuntleDbContext dbContext,
        IAdCampaignEligibilityService eligibilityService,
        CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns
            .AsNoTracking()
            .SingleAsync(item => item.Id == campaignId, cancellationToken);
        var productIds = await dbContext.AdCampaignProducts
            .AsNoTracking()
            .Where(product => product.AdCampaignId == campaign.Id)
            .OrderBy(product => product.CreatedAtUtc)
            .Select(product => product.ProductId)
            .ToListAsync(cancellationToken);
        var budget = await dbContext.AdBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AdCampaignId == campaign.Id, cancellationToken);
        var eligibility = await eligibilityService.ValidateAsync(campaign.SellerId, productIds, cancellationToken);
        var moderationEvents = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog => auditLog.EntityType == "AdCampaign" && auditLog.EntityId == campaign.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new SellerModerationEventResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new SellerAdCampaignResponse(
            campaign.Id,
            campaign.SellerId,
            campaign.Name,
            campaign.CampaignType.ToString(),
            campaign.Status.ToString(),
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            campaign.SubmittedAtUtc,
            campaign.ApprovedAtUtc,
            campaign.PausedAtUtc,
            campaign.CompletedAtUtc,
            campaign.CancelledAtUtc,
            campaign.RejectionReason,
            productIds,
            budget is null
                ? null
                : new AdBudgetResponse(
                    budget.Currency,
                    budget.DailyBudget,
                    budget.TotalBudget,
                    budget.MaxCostPerClick,
                    budget.SpentAmount),
            new AdCampaignEligibilityResponse(
                eligibility.IsEligible,
                eligibility.SellerReasons,
                eligibility.Products.Select(product => new AdProductEligibilityResponse(
                    product.ProductId,
                    product.IsEligible,
                    product.QualityScore,
                    product.Reasons)).ToArray()),
            moderationEvents);
    }

    private static async Task<AdCampaign?> GetOwnedCampaignAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        return seller is null
            ? null
            : await dbContext.AdCampaigns
                .AsNoTracking()
                .SingleOrDefaultAsync(campaign => campaign.Id == id && campaign.SellerId == seller.Id, cancellationToken);
    }

    private static async Task<AdCampaign?> GetOwnedCampaignForUpdateAsync(
        Guid id,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        return seller is null
            ? null
            : await dbContext.AdCampaigns
                .Include(campaign => campaign.Products)
                .SingleOrDefaultAsync(campaign => campaign.Id == id && campaign.SellerId == seller.Id, cancellationToken);
    }

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

    private static bool TryParseCampaignType(string campaignType, out AdCampaignType parsed) =>
        Enum.TryParse(campaignType, ignoreCase: true, out parsed);

    private static IResult InvalidCampaignType() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["campaignType"] = [$"Campaign type must be one of: {string.Join(", ", Enum.GetNames<AdCampaignType>())}."]
        });

    private static IResult EligibilityValidation(AdCampaignEligibilityResult eligibility) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["eligibility"] = eligibility.AllReasons.ToArray()
        });

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string detail) =>
        HttpResults.Problem(title: "AdCampaigns.InvalidState", detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "AdCampaigns.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult CampaignNotFound() =>
        HttpResults.Problem(
            title: "AdCampaigns.CampaignNotFound",
            detail: "Ad campaign was not found.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record UpsertSellerAdCampaignRequest(
    string Name,
    string CampaignType,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    IReadOnlyCollection<Guid>? ProductIds,
    UpsertAdBudgetRequest Budget);

public sealed record UpsertAdBudgetRequest(
    string Currency,
    decimal DailyBudget,
    decimal TotalBudget,
    decimal MaxCostPerClick);

public sealed record SellerAdCampaignResponse(
    Guid AdCampaignId,
    Guid SellerId,
    string Name,
    string CampaignType,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? RejectionReason,
    IReadOnlyCollection<Guid> ProductIds,
    AdBudgetResponse? Budget,
    AdCampaignEligibilityResponse Eligibility,
    IReadOnlyCollection<SellerModerationEventResponse> ModerationEvents);

public sealed record AdBudgetResponse(
    string Currency,
    decimal DailyBudget,
    decimal TotalBudget,
    decimal MaxCostPerClick,
    decimal SpentAmount);

public sealed record AdCampaignEligibilityResponse(
    bool IsEligible,
    IReadOnlyCollection<string> SellerReasons,
    IReadOnlyCollection<AdProductEligibilityResponse> Products);

public sealed record AdProductEligibilityResponse(
    Guid ProductId,
    bool IsEligible,
    int QualityScore,
    IReadOnlyCollection<string> Reasons);
