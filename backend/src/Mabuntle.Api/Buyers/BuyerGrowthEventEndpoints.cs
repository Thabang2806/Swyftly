using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Analytics;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Buyers;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Buyers;

public static class BuyerGrowthEventEndpoints
{
    public static IEndpointRouteBuilder MapBuyerGrowthEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/buyer/growth-events", RecordGrowthEventAsync)
            .WithTags("Buyer Growth Events")
            .WithName("RecordBuyerGrowthEvent")
            .WithSummary("Records a privacy-conscious buyer AI discovery event.")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly)
            .Produces<BuyerGrowthEventResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RecordGrowthEventAsync(
        BuyerGrowthEventRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IBuyerGrowthOutcomeAttributionService outcomeAttributionService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return HttpResults.NotFound();
        }

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        var eventType = Enum.Parse<BuyerGrowthEventType>(request.EventType, ignoreCase: true);
        var sourceTool = Enum.Parse<BuyerGrowthSourceTool>(request.SourceTool, ignoreCase: true);
        var confidenceBand = ParseOptional<BuyerGrowthConfidenceBand>(request.ConfidenceBand);
        var feedbackReason = ParseOptional<BuyerGrowthFeedbackReason>(request.FeedbackReason);

        if (request.ProductId.HasValue)
        {
            var productExists = await dbContext.Products
                .AsNoTracking()
                .AnyAsync(product => product.Id == request.ProductId.Value, cancellationToken);
            if (!productExists)
            {
                return HttpResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["productId"] = ["Product context was not found."]
                });
            }
        }

        var growthEvent = new BuyerGrowthEvent(
            buyer.Id,
            eventType,
            sourceTool,
            timeProvider.GetUtcNow(),
            request.ProductId,
            request.ResultCount,
            confidenceBand,
            request.Category,
            request.Colour,
            request.Material,
            request.SourceRoute,
            feedbackReason);

        dbContext.BuyerGrowthEvents.Add(growthEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (eventType is BuyerGrowthEventType.AssistantProductOpened or BuyerGrowthEventType.VisualProductOpened
            && request.ProductId.HasValue)
        {
            await outcomeAttributionService.RecordProductOpenedAsync(
                buyer.Id,
                request.ProductId.Value,
                growthEvent.Id,
                sourceTool,
                confidenceBand,
                growthEvent.OccurredAtUtc,
                cancellationToken);
        }

        return HttpResults.Accepted(value: new BuyerGrowthEventResponse(growthEvent.Id, "Recorded"));
    }

    private static Dictionary<string, string[]> Validate(BuyerGrowthEventRequest request)
    {
        var failures = new Dictionary<string, string[]>();

        if (!TryParseEnum<BuyerGrowthEventType>(request.EventType, out var eventType))
        {
            failures["eventType"] = ["eventType must be a known buyer growth event type."];
        }

        if (!TryParseEnum<BuyerGrowthSourceTool>(request.SourceTool, out var sourceTool))
        {
            failures["sourceTool"] = ["sourceTool must be Assistant or VisualSearch."];
        }

        if (eventType.HasValue && sourceTool.HasValue && !EventMatchesTool(eventType.Value, sourceTool.Value))
        {
            failures["sourceTool"] = ["sourceTool must match the event type family."];
        }

        if (request.ResultCount is < 0)
        {
            failures["resultCount"] = ["resultCount cannot be negative."];
        }

        if (!string.IsNullOrWhiteSpace(request.ConfidenceBand)
            && !TryParseEnum<BuyerGrowthConfidenceBand>(request.ConfidenceBand, out _))
        {
            failures["confidenceBand"] = ["confidenceBand must be High, Medium, or Low."];
        }

        if (!string.IsNullOrWhiteSpace(request.FeedbackReason)
            && !TryParseEnum<BuyerGrowthFeedbackReason>(request.FeedbackReason, out _))
        {
            failures["feedbackReason"] = ["feedbackReason must be GoodMatches, TooBroad, WrongStyle, WrongCategory, Unavailable, or LowConfidence."];
        }

        if (eventType is BuyerGrowthEventType.AssistantFeedbackSubmitted or BuyerGrowthEventType.VisualFeedbackSubmitted
            && string.IsNullOrWhiteSpace(request.FeedbackReason))
        {
            failures["feedbackReason"] = ["feedbackReason is required for feedback events."];
        }

        if (eventType is BuyerGrowthEventType.AssistantProductOpened or BuyerGrowthEventType.VisualProductOpened
            && !request.ProductId.HasValue)
        {
            failures["productId"] = ["productId is required for product-open events."];
        }

        AddLengthFailure(failures, request.Category, "category", BuyerGrowthEvent.ContextFieldMaxLength);
        AddLengthFailure(failures, request.Colour, "colour", BuyerGrowthEvent.ContextFieldMaxLength);
        AddLengthFailure(failures, request.Material, "material", BuyerGrowthEvent.ContextFieldMaxLength);
        AddLengthFailure(failures, request.SourceRoute, "sourceRoute", BuyerGrowthEvent.SourceRouteMaxLength);

        return failures;
    }

    private static bool EventMatchesTool(BuyerGrowthEventType eventType, BuyerGrowthSourceTool sourceTool) =>
        sourceTool switch
        {
            BuyerGrowthSourceTool.Assistant => eventType is BuyerGrowthEventType.AssistantSearchSubmitted
                or BuyerGrowthEventType.AssistantProductOpened
                or BuyerGrowthEventType.AssistantShopHandoff
                or BuyerGrowthEventType.AssistantFeedbackSubmitted,
            BuyerGrowthSourceTool.VisualSearch => eventType is BuyerGrowthEventType.VisualSearchSubmitted
                or BuyerGrowthEventType.VisualProductOpened
                or BuyerGrowthEventType.VisualShopHandoff
                or BuyerGrowthEventType.VisualFeedbackSubmitted,
            _ => false
        };

    private static void AddLengthFailure(
        IDictionary<string, string[]> failures,
        string? value,
        string fieldName,
        int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            failures[fieldName] = [$"{fieldName} must be {maxLength} characters or fewer."];
        }
    }

    private static TEnum? ParseOptional<TEnum>(string? value)
        where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<TEnum>(value, ignoreCase: true);

    private static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

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
}

public sealed record BuyerGrowthEventRequest(
    string EventType,
    string SourceTool,
    Guid? ProductId,
    int? ResultCount,
    string? ConfidenceBand,
    string? Category,
    string? Colour,
    string? Material,
    string? SourceRoute,
    string? FeedbackReason);

public sealed record BuyerGrowthEventResponse(Guid EventId, string Status);
