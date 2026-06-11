using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Notifications;
using Mabuntle.Api.Results;
using Mabuntle.Api.Sellers;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Returns;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Returns;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;
using Mabuntle.Infrastructure.Returns;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Returns;

public static class ReturnEndpoints
{
    public static IEndpointRouteBuilder MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/buyer")
            .WithTags("Buyer Returns")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly);

        buyerGroup.MapPost("/orders/{orderId:guid}/returns", CreateReturnAsync)
            .WithName("CreateBuyerReturn")
            .WithSummary("Creates a return request for a delivered buyer order.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        buyerGroup.MapGet("/returns", GetBuyerReturnsAsync)
            .WithName("GetBuyerReturns")
            .WithSummary("Returns return requests owned by the authenticated buyer.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("/returns/{returnRequestId:guid}", GetBuyerReturnAsync)
            .WithName("GetBuyerReturn")
            .WithSummary("Returns one return request owned by the authenticated buyer.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapPost("/returns/{returnRequestId:guid}/dispute", DisputeReturnAsync)
            .WithName("DisputeBuyerReturn")
            .WithSummary("Escalates a rejected buyer return to dispute status.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sellerGroup = app.MapGroup("/api/seller/returns")
            .WithTags("Seller Returns")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        sellerGroup.MapGet("", GetSellerReturnsAsync)
            .WithName("GetSellerReturns")
            .WithSummary("Returns return requests for the authenticated seller.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("/{returnRequestId:guid}", GetSellerReturnAsync)
            .WithName("GetSellerReturn")
            .WithSummary("Returns one return request for the authenticated seller.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{returnRequestId:guid}/approve", ApproveReturnAsync)
            .WithName("ApproveSellerReturn")
            .WithSummary("Approves a return request awaiting seller response.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapPost("/{returnRequestId:guid}/reject", RejectReturnAsync)
            .WithName("RejectSellerReturn")
            .WithSummary("Rejects a return request awaiting seller response.")
            .Produces<ReturnRequestResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sellerGroup.MapGet("/{returnRequestId:guid}/restock-decisions", GetSellerReturnRestockDecisionsAsync)
            .WithName("GetSellerReturnRestockDecisions")
            .WithSummary("Lists seller restock decisions recorded for a return request.")
            .Produces<IReadOnlyCollection<SellerReturnRestockDecisionResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{returnRequestId:guid}/restock-decisions", CreateSellerReturnRestockDecisionsAsync)
            .WithName("CreateSellerReturnRestockDecisions")
            .WithSummary("Records seller restock decisions for returned items without changing return/refund state.")
            .Produces<IReadOnlyCollection<SellerReturnRestockDecisionResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var adminGroup = app.MapGroup("/api/admin/returns")
            .WithTags("Admin Returns")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        adminGroup.MapGet("/disputed", GetDisputedReturnsAsync)
            .WithName("GetDisputedReturns")
            .WithSummary("Returns disputed return requests for admin review.")
            .Produces<IReadOnlyCollection<ReturnRequestResult>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> CreateReturnAsync(
        Guid orderId,
        CreateReturnRequestApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.RequestReturnAsync(
            new CreateReturnRequest(
                buyer.Id,
                buyerUserId,
                orderId,
                request.Reason,
                request.Details,
                request.Items.Select(item => new CreateReturnItemRequest(
                    item.OrderItemId,
                    item.Quantity,
                    item.Reason,
                    item.IsOpenedOrUnsealed,
                    item.Note)).ToArray(),
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetBuyerReturnsAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.BuyerId == buyer.Id)
            .OrderByDescending(returnRequest => returnRequest.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(await MapReturnsAsync(returns, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetBuyerReturnAsync(
        Guid returnRequestId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var returnRequest = await ReturnQuery(dbContext)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.BuyerId == buyer.Id,
                cancellationToken);

        return returnRequest is null
            ? ReturnNotFound()
            : HttpResults.Ok(await MapReturnAsync(returnRequest, dbContext, cancellationToken));
    }

    private static async Task<IResult> DisputeReturnAsync(
        Guid returnRequestId,
        DisputeReturnRequestApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var buyerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.DisputeReturnAsync(
            new BuyerReturnDisputeRequest(
                buyer.Id,
                buyerUserId,
                returnRequestId,
                request.Reason,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetSellerReturnsAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.SellerId == seller.Id)
            .OrderByDescending(returnRequest => returnRequest.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(await MapReturnsAsync(returns, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetSellerReturnAsync(
        Guid returnRequestId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var returnRequest = await ReturnQuery(dbContext)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.SellerId == seller.Id,
                cancellationToken);

        return returnRequest is null
            ? ReturnNotFound()
            : HttpResults.Ok(await MapReturnAsync(returnRequest, dbContext, cancellationToken));
    }

    private static async Task<IResult> ApproveReturnAsync(
        Guid returnRequestId,
        SellerReturnResponseApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var sellerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.ApproveReturnAsync(
            new SellerReturnResponseRequest(
                seller.Id,
                sellerUserId,
                returnRequestId,
                request.Message,
                timeProvider.GetUtcNow()),
            cancellationToken);

        if (result.IsSuccess)
        {
            await BuyerNotificationDispatcher.NotifyBuyerAsync(
                result.Value.BuyerId,
                "ReturnApproved",
                "Your return was approved",
                "The seller approved your return request.",
                "ReturnRequest",
                result.Value.ReturnRequestId,
                timeProvider.GetUtcNow(),
                dbContext,
                notificationService,
                loggerFactory.CreateLogger(nameof(ReturnEndpoints)),
                cancellationToken);
        }

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> RejectReturnAsync(
        Guid returnRequestId,
        SellerReturnResponseApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IReturnWorkflowService returnWorkflowService,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var sellerUserId))
        {
            return UserNotFound();
        }

        var result = await returnWorkflowService.RejectReturnAsync(
            new SellerReturnResponseRequest(
                seller.Id,
                sellerUserId,
                returnRequestId,
                request.Message,
                timeProvider.GetUtcNow()),
            cancellationToken);

        if (result.IsSuccess)
        {
            await BuyerNotificationDispatcher.NotifyBuyerAsync(
                result.Value.BuyerId,
                "ReturnRejected",
                "Your return was rejected",
                "The seller rejected your return request. You can review the seller response in your account.",
                "ReturnRequest",
                result.Value.ReturnRequestId,
                timeProvider.GetUtcNow(),
                dbContext,
                notificationService,
                loggerFactory.CreateLogger(nameof(ReturnEndpoints)),
                cancellationToken);
        }

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetSellerReturnRestockDecisionsAsync(
        Guid returnRequestId,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var returnExists = await dbContext.ReturnRequests
            .AsNoTracking()
            .AnyAsync(
                returnRequest => returnRequest.Id == returnRequestId && returnRequest.SellerId == seller.Id,
                cancellationToken);
        if (!returnExists)
        {
            return ReturnNotFound();
        }

        var decisions = await CreateRestockDecisionQuery(dbContext, seller.Id, returnRequestId)
            .ToArrayAsync(cancellationToken);

        return HttpResults.Ok(decisions);
    }

    private static async Task<IResult> CreateSellerReturnRestockDecisionsAsync(
        Guid returnRequestId,
        SellerReturnRestockDecisionApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var sellerUserId))
        {
            return UserNotFound();
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Validation("items", "At least one restock decision item is required.");
        }

        if (request.Items.Count > 50)
        {
            return Validation("items", "A restock decision request cannot contain more than 50 items.");
        }

        var duplicateRequestedItemId = request.Items
            .GroupBy(item => item.ReturnItemId)
            .Where(group => group.Key != Guid.Empty && group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateRequestedItemId != Guid.Empty)
        {
            return Validation("items", "Each return item can appear only once in a restock decision request.");
        }

        var returnRequest = await dbContext.ReturnRequests
            .Include(existing => existing.Items)
            .SingleOrDefaultAsync(
                existing => existing.Id == returnRequestId && existing.SellerId == seller.Id,
                cancellationToken);
        if (returnRequest is null)
        {
            return ReturnNotFound();
        }

        if (!CanRecordRestockDecision(returnRequest.Status))
        {
            return Conflict(
                "Returns.RestockNotAllowed",
                "Restock decisions can only be recorded after a return has been approved, returned, refunded, or closed.");
        }

        var returnItemsById = returnRequest.Items.ToDictionary(item => item.Id);
        foreach (var item in request.Items)
        {
            if (item.ReturnItemId == Guid.Empty || !returnItemsById.TryGetValue(item.ReturnItemId, out var returnItem))
            {
                return Validation("items", "Every restock decision item must reference an item from this return request.");
            }

            if (item.QuantityRestocked < 0 || item.QuantityRestocked > returnItem.Quantity)
            {
                return Validation("quantityRestocked", "Quantity restocked must be between zero and the returned item quantity.");
            }

            if (!Enum.TryParse<ReturnRestockCondition>(item.Condition, ignoreCase: true, out var condition)
                || !Enum.IsDefined(condition))
            {
                return Validation("condition", $"Condition must be one of: {string.Join(", ", Enum.GetNames<ReturnRestockCondition>())}.");
            }

            var reason = item.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Validation("reason", "Restock decision reason is required.");
            }

            if (reason.Length > ReturnRestockDecision.ReasonMaxLength)
            {
                return Validation("reason", $"Restock decision reason cannot exceed {ReturnRestockDecision.ReasonMaxLength} characters.");
            }
        }

        var requestedItemIds = request.Items.Select(item => item.ReturnItemId).ToArray();
        var existingDecisionItemId = await dbContext.ReturnRestockDecisions
            .AsNoTracking()
            .Where(decision => requestedItemIds.Contains(decision.ReturnItemId))
            .Select(decision => decision.ReturnItemId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingDecisionItemId != Guid.Empty)
        {
            return Conflict(
                "Returns.RestockDecisionExists",
                "A restock decision has already been recorded for one or more selected return items.");
        }

        var variantIds = request.Items
            .Select(item => returnItemsById[item.ReturnItemId].ProductVariantId)
            .Distinct()
            .ToArray();
        var variants = await dbContext.ProductVariants
            .Where(variant => variantIds.Contains(variant.Id))
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);

        var decidedAtUtc = timeProvider.GetUtcNow();
        var batchReference = request.Items.Count > 1 ? $"return-restock-{Guid.NewGuid():N}" : null;

        foreach (var item in request.Items)
        {
            var returnItem = returnItemsById[item.ReturnItemId];
            var variant = variants[returnItem.ProductVariantId];
            var condition = Enum.Parse<ReturnRestockCondition>(item.Condition, ignoreCase: true);
            var reason = item.Reason!.Trim();

            var previousStockQuantity = variant.StockQuantity;
            var previousReservedQuantity = variant.ReservedQuantity;
            var previousStatus = variant.Status;
            if (item.QuantityRestocked > 0)
            {
                variant.AdjustInventory(variant.StockQuantity + item.QuantityRestocked, variant.Status);
                dbContext.InventoryMovements.Add(new InventoryMovement(
                    seller.Id,
                    returnItem.ProductId,
                    returnItem.ProductVariantId,
                    InventoryMovementType.ReturnRestocked,
                    previousStockQuantity,
                    variant.StockQuantity,
                    previousReservedQuantity,
                    variant.ReservedQuantity,
                    previousStatus,
                    variant.Status,
                    "SellerReturnRestock",
                    reason,
                    sellerUserId,
                    batchReference,
                    decidedAtUtc,
                    orderId: returnRequest.OrderId,
                    returnRequestId: returnRequest.Id));
            }

            var decision = new ReturnRestockDecision(
                seller.Id,
                returnRequest.Id,
                returnItem.Id,
                returnItem.ProductId,
                returnItem.ProductVariantId,
                item.QuantityRestocked,
                condition,
                reason,
                sellerUserId,
                decidedAtUtc);
            dbContext.ReturnRestockDecisions.Add(decision);

            await auditLogService.RecordAsync(
                new CreateAuditLogEntry(
                    sellerUserId.ToString(),
                    "Seller",
                    "SellerReturnRestockDecisionRecorded",
                    "ReturnItem",
                    returnItem.Id.ToString(),
                    JsonSerializer.Serialize(new
                    {
                        StockQuantity = previousStockQuantity,
                        ReservedQuantity = previousReservedQuantity,
                        Status = previousStatus.ToString()
                    }),
                    JsonSerializer.Serialize(new
                    {
                        item.QuantityRestocked,
                        Condition = condition.ToString(),
                        reason,
                        StockQuantityAfter = variant.StockQuantity
                    }),
                    reason),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var decisions = await CreateRestockDecisionQuery(dbContext, seller.Id, returnRequestId)
            .ToArrayAsync(cancellationToken);

        return HttpResults.Ok(decisions);
    }

    private static async Task<IResult> GetDisputedReturnsAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var returns = await ReturnQuery(dbContext)
            .Where(returnRequest => returnRequest.Status == ReturnStatus.Disputed)
            .OrderBy(returnRequest => returnRequest.DisputedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(await MapReturnsAsync(returns, dbContext, cancellationToken));
    }

    private static async Task<ReturnRequestResult> MapReturnAsync(
        ReturnRequest returnRequest,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == returnRequest.OrderId, cancellationToken);

        return EfReturnWorkflowService.Map(
            returnRequest,
            SellerPolicyResponseMapper.MapSnapshot(order?.SellerPolicySnapshot));
    }

    private static async Task<IReadOnlyCollection<ReturnRequestResult>> MapReturnsAsync(
        IReadOnlyCollection<ReturnRequest> returnRequests,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var orderIds = returnRequests.Select(returnRequest => returnRequest.OrderId).Distinct().ToArray();
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .ToDictionaryAsync(order => order.Id, cancellationToken);

        return returnRequests
            .Select(returnRequest => EfReturnWorkflowService.Map(
                returnRequest,
                orders.TryGetValue(returnRequest.OrderId, out var order)
                    ? SellerPolicyResponseMapper.MapSnapshot(order.SellerPolicySnapshot)
                    : null))
            .ToArray();
    }

    private static IQueryable<ReturnRequest> ReturnQuery(MabuntleDbContext dbContext) =>
        dbContext.ReturnRequests
            .Include(returnRequest => returnRequest.Items)
            .Include(returnRequest => returnRequest.Messages)
            .AsNoTracking();

    private static IQueryable<SellerReturnRestockDecisionResponse> CreateRestockDecisionQuery(
        MabuntleDbContext dbContext,
        Guid sellerId,
        Guid returnRequestId) =>
        dbContext.ReturnRestockDecisions
            .AsNoTracking()
            .Where(decision => decision.SellerId == sellerId && decision.ReturnRequestId == returnRequestId)
            .Join(
                dbContext.ReturnItems.AsNoTracking(),
                decision => decision.ReturnItemId,
                item => item.Id,
                (decision, item) => new { Decision = decision, Item = item })
            .Join(
                dbContext.ProductVariants.AsNoTracking(),
                item => item.Decision.ProductVariantId,
                variant => variant.Id,
                (item, variant) => new { item.Decision, item.Item, Variant = variant })
            .OrderBy(item => item.Decision.CreatedAtUtc)
            .Select(item => new SellerReturnRestockDecisionResponse(
                item.Decision.Id,
                item.Decision.ReturnRequestId,
                item.Decision.ReturnItemId,
                item.Decision.ProductId,
                item.Decision.ProductVariantId,
                item.Variant.Sku,
                item.Variant.Size,
                item.Variant.Colour,
                item.Item.Quantity,
                item.Decision.QuantityRestocked,
                item.Decision.Condition.ToString(),
                item.Decision.Reason,
                item.Decision.ActorUserId,
                item.Decision.CreatedAtUtc));

    private static bool CanRecordRestockDecision(ReturnStatus status) =>
        status is ReturnStatus.Approved
            or ReturnStatus.ReturnedToSeller
            or ReturnStatus.RefundPending
            or ReturnStatus.Refunded
            or ReturnStatus.Closed;

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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Returns.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult SellerNotFound() =>
        HttpResults.Problem(
            title: "Returns.SellerNotFound",
            detail: "The authenticated user does not have a seller profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult UserNotFound() =>
        HttpResults.Problem(
            title: "Returns.UserNotFound",
            detail: "The authenticated user id could not be resolved.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ReturnNotFound() =>
        HttpResults.Problem(
            title: "Returns.NotFound",
            detail: "Return request was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string title, string detail) =>
        HttpResults.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
}

public sealed record CreateReturnRequestApiRequest(
    string Reason,
    string? Details,
    IReadOnlyCollection<CreateReturnItemApiRequest> Items);

public sealed record CreateReturnItemApiRequest(
    Guid OrderItemId,
    int Quantity,
    string Reason,
    bool IsOpenedOrUnsealed,
    string? Note);

public sealed record SellerReturnResponseApiRequest(string? Message);

public sealed record DisputeReturnRequestApiRequest(string Reason);

public sealed record SellerReturnRestockDecisionApiRequest(
    IReadOnlyCollection<SellerReturnRestockDecisionItemApiRequest> Items);

public sealed record SellerReturnRestockDecisionItemApiRequest(
    Guid ReturnItemId,
    int QuantityRestocked,
    string Condition,
    string Reason);

public sealed record SellerReturnRestockDecisionResponse(
    Guid RestockDecisionId,
    Guid ReturnRequestId,
    Guid ReturnItemId,
    Guid ProductId,
    Guid ProductVariantId,
    string Sku,
    string Size,
    string Colour,
    int QuantityReturned,
    int QuantityRestocked,
    string Condition,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset CreatedAtUtc);
