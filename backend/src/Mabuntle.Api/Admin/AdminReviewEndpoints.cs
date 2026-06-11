using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Notifications;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminReviewEndpoints
{
    public static IEndpointRouteBuilder MapAdminReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reviews")
            .WithTags("Admin Reviews")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingProductReviews")
            .WithSummary("Returns verified-buyer product reviews waiting for moderation.")
            .Produces<IReadOnlyCollection<AdminProductReviewDetailResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{reviewId:guid}", GetByIdAsync)
            .WithName("GetAdminProductReview")
            .WithSummary("Returns product review moderation detail.")
            .Produces<AdminProductReviewDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{reviewId:guid}/approve", ApproveAsync)
            .WithName("ApproveProductReview")
            .WithSummary("Publishes a verified-buyer product review.")
            .Produces<AdminProductReviewDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{reviewId:guid}/reject", RejectAsync)
            .WithName("RejectProductReview")
            .WithSummary("Rejects a verified-buyer product review with a moderation reason.")
            .Produces<AdminProductReviewDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{reviewId:guid}/remove", RemoveAsync)
            .WithName("RemoveProductReview")
            .WithSummary("Removes a product review from public and buyer review surfaces.")
            .Produces<AdminProductReviewDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPendingAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var reviewIds = await dbContext.ProductReviews
            .AsNoTracking()
            .Where(review => review.Status == ProductReviewStatus.PendingReview)
            .OrderBy(review => review.CreatedAtUtc)
            .Select(review => review.Id)
            .ToListAsync(cancellationToken);

        var responses = new List<AdminProductReviewDetailResponse>();
        foreach (var reviewId in reviewIds)
        {
            var detail = await CreateDetailResponseAsync(reviewId, dbContext, cancellationToken);
            if (detail is not null)
            {
                responses.Add(detail);
            }
        }

        return HttpResults.Ok(responses);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid reviewId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var detail = await CreateDetailResponseAsync(reviewId, dbContext, cancellationToken);
        return detail is null ? ReviewNotFound() : HttpResults.Ok(detail);
    }

    private static async Task<IResult> ApproveAsync(
        Guid reviewId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var review = await dbContext.ProductReviews.SingleOrDefaultAsync(existing => existing.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return ReviewNotFound();
        }

        var previousStatus = review.Status;
        try
        {
            review.Approve(actorUserId, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductReviewApproved",
            review.Id,
            previousStatus,
            review.Status,
            null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await BuyerNotificationDispatcher.NotifyBuyerAsync(
            review.BuyerId,
            "ReviewApproved",
            "Your review was published",
            "Your verified-purchase review is now visible on the product page.",
            "ProductReview",
            review.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminReviewEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(review.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(
        Guid reviewId,
        AdminProductReviewReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReasonRequired();
        }

        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var review = await dbContext.ProductReviews.SingleOrDefaultAsync(existing => existing.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return ReviewNotFound();
        }

        var previousStatus = review.Status;
        try
        {
            review.Reject(request.Reason, actorUserId, timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Validation("reason", exception.Message);
        }

        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductReviewRejected",
            review.Id,
            previousStatus,
            review.Status,
            request.Reason,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await BuyerNotificationDispatcher.NotifyBuyerAsync(
            review.BuyerId,
            "ReviewRejected",
            "Your review needs changes",
            $"Your review was not published. Reason: {review.ModerationReason}",
            "ProductReview",
            review.Id,
            timeProvider.GetUtcNow(),
            dbContext,
            notificationService,
            loggerFactory.CreateLogger(nameof(AdminReviewEndpoints)),
            cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(review.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> RemoveAsync(
        Guid reviewId,
        AdminProductReviewReasonRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReasonRequired();
        }

        if (!TryGetUserId(principal, out var actorUserId))
        {
            return UserNotFound();
        }

        var review = await dbContext.ProductReviews.SingleOrDefaultAsync(existing => existing.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return ReviewNotFound();
        }

        var previousStatus = review.Status;
        review.Remove(timeProvider.GetUtcNow(), actorUserId, request.Reason);
        await AddAuditLogAsync(
            auditLogService,
            principal,
            httpContext,
            "ProductReviewRemoved",
            review.Id,
            previousStatus,
            review.Status,
            request.Reason,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await CreateDetailResponseAsync(review.Id, dbContext, cancellationToken));
    }

    private static async Task<AdminProductReviewDetailResponse?> CreateDetailResponseAsync(
        Guid reviewId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.ProductReviews
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return null;
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == review.ProductId, cancellationToken);
        var seller = await dbContext.SellerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == review.SellerId, cancellationToken);
        var buyer = await dbContext.BuyerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == review.BuyerId, cancellationToken);
        var order = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == review.OrderId, cancellationToken);
        var orderItem = await dbContext.OrderItems
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == review.OrderItemId, cancellationToken);
        var primaryImage = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => image.ProductId == review.ProductId)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        var auditTrail = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog => auditLog.EntityType == "ProductReview" && auditLog.EntityId == review.Id.ToString())
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActionType,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Reason,
                auditLog.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminProductReviewDetailResponse(
            review.Id,
            review.BuyerId,
            review.SellerId,
            review.ProductId,
            review.OrderId,
            review.OrderItemId,
            review.Rating,
            review.Title,
            review.Body,
            review.Status.ToString(),
            review.ModerationReason,
            review.ModeratedByUserId,
            review.ModeratedAtUtc,
            review.CreatedAtUtc,
            review.UpdatedAtUtc,
            new AdminProductReviewProductResponse(
                product?.Title,
                product?.Slug,
                product?.CategoryId,
                primaryImage?.Url,
                primaryImage?.AltText),
            new AdminProductReviewSellerResponse(
                seller?.DisplayName,
                seller?.ContactEmail,
                seller?.VerificationStatus.ToString()),
            new AdminProductReviewBuyerResponse(
                buyer?.UserId),
            new AdminProductReviewOrderResponse(
                order?.Status.ToString(),
                order?.TotalAmount,
                orderItem?.ProductTitle,
                orderItem?.Sku,
                orderItem?.Size,
                orderItem?.Colour,
                orderItem?.Quantity),
            auditTrail);
    }

    private static async Task AddAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        Guid reviewId,
        ProductReviewStatus previousStatus,
        ProductReviewStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                GetActorRole(principal),
                actionType,
                "ProductReview",
                reviewId.ToString(),
                JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
                JsonSerializer.Serialize(new { status = newStatus.ToString() }),
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

    private static IResult ReasonRequired() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reason"] = ["Reason is required."]
        });

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string detail) =>
        HttpResults.Problem(
            title: "ProductReviews.InvalidState",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);

    private static IResult ReviewNotFound() =>
        HttpResults.Problem(
            title: "ProductReviews.NotFound",
            detail: "Product review was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult UserNotFound() =>
        HttpResults.Problem(
            title: "ProductReviews.UserNotFound",
            detail: "The authenticated user id could not be resolved.",
            statusCode: StatusCodes.Status404NotFound);
}

public sealed record AdminProductReviewDetailResponse(
    Guid ReviewId,
    Guid BuyerId,
    Guid SellerId,
    Guid ProductId,
    Guid OrderId,
    Guid OrderItemId,
    int Rating,
    string? Title,
    string? Body,
    string Status,
    string? ModerationReason,
    Guid? ModeratedByUserId,
    DateTimeOffset? ModeratedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    AdminProductReviewProductResponse Product,
    AdminProductReviewSellerResponse Seller,
    AdminProductReviewBuyerResponse Buyer,
    AdminProductReviewOrderResponse Order,
    IReadOnlyCollection<AdminAuditLogResponse> AuditTrail);

public sealed record AdminProductReviewProductResponse(
    string? Title,
    string? Slug,
    Guid? CategoryId,
    string? PrimaryImageUrl,
    string? PrimaryImageAltText);

public sealed record AdminProductReviewSellerResponse(
    string? DisplayName,
    string? ContactEmail,
    string? VerificationStatus);

public sealed record AdminProductReviewBuyerResponse(
    Guid? UserId);

public sealed record AdminProductReviewOrderResponse(
    string? Status,
    decimal? TotalAmount,
    string? ProductTitle,
    string? Sku,
    string? Size,
    string? Colour,
    int? Quantity);

public sealed record AdminProductReviewReasonRequest(string Reason);
