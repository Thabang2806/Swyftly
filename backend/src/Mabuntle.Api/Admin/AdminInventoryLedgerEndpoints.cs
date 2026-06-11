using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Domain.Refunds;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminInventoryLedgerEndpoints
{
    private const int DefaultBatchSize = 500;
    private const int MaxBatchSize = 2000;
    private const string BackfillSource = "InventoryLedgerBackfill";

    public static IEndpointRouteBuilder MapAdminInventoryLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/inventory-ledger")
            .WithTags("Admin Inventory Ledger")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapPost("/backfill", BackfillInventoryLedgerAsync)
            .WithName("BackfillAdminInventoryLedger")
            .WithSummary("Backfills missing seller stock-ledger movements from historical operational records.")
            .Produces<AdminInventoryLedgerBackfillResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> BackfillInventoryLedgerAsync(
        AdminInventoryLedgerBackfillRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc.Value > request.ToUtc.Value)
        {
            return Validation("dateRange", "fromUtc must be earlier than toUtc.");
        }

        var batchSize = request.BatchSize ?? DefaultBatchSize;
        if (batchSize <= 0 || batchSize > MaxBatchSize)
        {
            return Validation("batchSize", $"batchSize must be between 1 and {MaxBatchSize}.");
        }

        if (request.SellerId.HasValue)
        {
            var sellerExists = await dbContext.SellerProfiles
                .AsNoTracking()
                .AnyAsync(seller => seller.Id == request.SellerId.Value, cancellationToken);
            if (!sellerExists)
            {
                return Validation("sellerId", "Seller was not found.");
            }
        }

        var context = new BackfillContext(
            request.DryRun,
            request.SellerId,
            request.FromUtc,
            request.ToUtc,
            batchSize);

        await BackfillReservationsAsync(context, dbContext, cancellationToken);
        await BackfillReturnsAsync(context, dbContext, cancellationToken);
        await BackfillRefundsAsync(context, dbContext, cancellationToken);

        var response = context.ToResponse();

        if (!request.DryRun)
        {
            await auditLogService.RecordAsync(
                new CreateAuditLogEntry(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    principal.IsInRole(MabuntleRoles.SuperAdmin) ? MabuntleRoles.SuperAdmin : MabuntleRoles.Admin,
                    "InventoryLedgerBackfilled",
                    "InventoryMovement",
                    request.SellerId?.ToString(),
                    PreviousValueJson: null,
                    JsonSerializer.Serialize(response),
                    "Historical stock-ledger backfill was applied."),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return HttpResults.Ok(response);
    }

    private static async Task BackfillReservationsAsync(
        BackfillContext context,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryReservations
            .AsNoTracking()
            .Join(
                dbContext.ProductVariants.AsNoTracking(),
                reservation => reservation.ProductVariantId,
                variant => variant.Id,
                (reservation, variant) => new { Reservation = reservation, Variant = variant })
            .Join(
                dbContext.Products.AsNoTracking(),
                item => item.Variant.ProductId,
                product => product.Id,
                (item, product) => new ReservationBackfillProjection(
                    item.Reservation.Id,
                    item.Reservation.CartId,
                    item.Reservation.ProductVariantId,
                    item.Reservation.Quantity,
                    item.Reservation.Status,
                    item.Reservation.CreatedAtUtc,
                    item.Reservation.ConfirmedAtUtc,
                    item.Reservation.ExpiredAtUtc,
                    item.Reservation.CancelledAtUtc,
                    product.SellerId,
                    product.Id,
                    item.Variant.StockQuantity,
                    item.Variant.Status));

        if (context.SellerId.HasValue)
        {
            query = query.Where(item => item.SellerId == context.SellerId.Value);
        }

        var reservations = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var cartIds = reservations.Select(item => item.CartId).Distinct().ToArray();
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => cartIds.Contains(order.CartId))
            .ToListAsync(cancellationToken);
        var orderIds = orders.Select(order => order.Id).ToArray();
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => orderIds.Contains(payment.OrderId))
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var ordersByCartId = orders
            .GroupBy(order => order.CartId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(order => order.CreatedAtUtc).ToArray());
        var paymentsByOrderId = payments
            .GroupBy(payment => payment.OrderId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var reservation in reservations)
        {
            if (!context.HasCapacity)
            {
                return;
            }

            await TryAddReservationMovementAsync(
                context,
                dbContext,
                reservation,
                InventoryMovementType.ReservationCreated,
                reservation.CreatedAtUtc,
                "Historical backfill reconstructed checkout reservation creation.",
                orderId: null,
                paymentId: null,
                cancellationToken);

            if (!context.HasCapacity)
            {
                return;
            }

            if (reservation.Status == InventoryReservationStatus.Confirmed && reservation.ConfirmedAtUtc.HasValue)
            {
                var order = FindOrderForCart(ordersByCartId, reservation.CartId);
                var payment = order is null
                    ? null
                    : FindPaymentForOrder(paymentsByOrderId, order.Id, PaymentStatus.Paid);
                if (order is null || payment is null)
                {
                    context.SkipAmbiguous($"Reservation {reservation.ReservationId} is confirmed but its paid order/payment could not be derived.");
                    continue;
                }

                await TryAddReservationMovementAsync(
                    context,
                    dbContext,
                    reservation,
                    InventoryMovementType.ReservationConfirmed,
                    reservation.ConfirmedAtUtc.Value,
                    "Historical backfill reconstructed payment-confirmed reservation context; stock quantity was not deducted automatically.",
                    order.Id,
                    payment.Id,
                    cancellationToken);
            }
            else if (reservation.Status == InventoryReservationStatus.Expired && reservation.ExpiredAtUtc.HasValue)
            {
                var order = FindOrderForCart(ordersByCartId, reservation.CartId);
                await TryAddReservationMovementAsync(
                    context,
                    dbContext,
                    reservation,
                    InventoryMovementType.ReservationExpired,
                    reservation.ExpiredAtUtc.Value,
                    "Historical backfill reconstructed checkout reservation expiry.",
                    order?.Id,
                    paymentId: null,
                    cancellationToken);
            }
            else if (reservation.Status == InventoryReservationStatus.Cancelled && reservation.CancelledAtUtc.HasValue)
            {
                var order = FindOrderForCart(ordersByCartId, reservation.CartId);
                var failedPayment = order is null
                    ? null
                    : FindPaymentForOrder(paymentsByOrderId, order.Id, PaymentStatus.Failed, PaymentStatus.Cancelled);
                if (order is not null && failedPayment is not null && order.Status == OrderStatus.Cancelled)
                {
                    await TryAddReservationMovementAsync(
                        context,
                        dbContext,
                        reservation,
                        InventoryMovementType.PaymentFailedReservationReleased,
                        reservation.CancelledAtUtc.Value,
                        "Historical backfill reconstructed payment-failed reservation release.",
                        order.Id,
                        failedPayment.Id,
                        cancellationToken);
                }
                else
                {
                    await TryAddReservationMovementAsync(
                        context,
                        dbContext,
                        reservation,
                        InventoryMovementType.ReservationReleased,
                        reservation.CancelledAtUtc.Value,
                        "Historical backfill reconstructed checkout reservation refresh release.",
                        order?.Id,
                        paymentId: null,
                        cancellationToken);
                }
            }
        }
    }

    private static async Task TryAddReservationMovementAsync(
        BackfillContext context,
        MabuntleDbContext dbContext,
        ReservationBackfillProjection reservation,
        InventoryMovementType movementType,
        DateTimeOffset occurredAtUtc,
        string reason,
        Guid? orderId,
        Guid? paymentId,
        CancellationToken cancellationToken)
    {
        if (!context.ShouldScan(occurredAtUtc))
        {
            return;
        }

        context.MarkScanned();
        var exists = await dbContext.InventoryMovements.AnyAsync(
            movement => movement.MovementType == movementType
                && movement.ReservationId == reservation.ReservationId
                && (!paymentId.HasValue || movement.PaymentId == paymentId.Value),
            cancellationToken);
        if (exists)
        {
            context.SkipExisting();
            return;
        }

        var reservedBefore = movementType switch
        {
            InventoryMovementType.ReservationCreated => 0,
            InventoryMovementType.ReservationConfirmed => reservation.Quantity,
            _ => reservation.Quantity
        };
        var reservedAfter = movementType switch
        {
            InventoryMovementType.ReservationCreated => reservation.Quantity,
            InventoryMovementType.ReservationConfirmed => reservation.Quantity,
            _ => 0
        };

        context.AddMovement(new InventoryMovement(
            reservation.SellerId,
            reservation.ProductId,
            reservation.ProductVariantId,
            movementType,
            reservation.StockQuantity,
            reservation.StockQuantity,
            reservedBefore,
            reservedAfter,
            reservation.VariantStatus,
            reservation.VariantStatus,
            BackfillSource,
            reason,
            actorUserId: null,
            batchReference: null,
            occurredAtUtc,
            cartId: reservation.CartId,
            orderId,
            reservationId: reservation.ReservationId,
            paymentId));
        if (!context.DryRun)
        {
            dbContext.InventoryMovements.Add(context.LastMovement!);
        }
    }

    private static async Task BackfillReturnsAsync(
        BackfillContext context,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!context.HasCapacity)
        {
            return;
        }

        var query = dbContext.ReturnItems
            .AsNoTracking()
            .Join(
                dbContext.ReturnRequests.AsNoTracking(),
                item => item.ReturnRequestId,
                request => request.Id,
                (item, request) => new { Item = item, Request = request })
            .Join(
                dbContext.ProductVariants.AsNoTracking(),
                item => item.Item.ProductVariantId,
                variant => variant.Id,
                (item, variant) => new { item.Item, item.Request, Variant = variant })
            .Join(
                dbContext.Products.AsNoTracking(),
                item => item.Item.ProductId,
                product => product.Id,
                (item, product) => new ReturnBackfillProjection(
                    item.Request.Id,
                    item.Request.OrderId,
                    item.Request.SellerId,
                    item.Request.RequestedAtUtc,
                    item.Item.ProductId,
                    item.Item.ProductVariantId,
                    item.Variant.StockQuantity,
                    item.Variant.ReservedQuantity,
                    item.Variant.Status,
                    product.SellerId));

        if (context.SellerId.HasValue)
        {
            query = query.Where(item => item.ReturnSellerId == context.SellerId.Value);
        }

        var returns = await query
            .OrderBy(item => item.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var item in returns)
        {
            if (!context.HasCapacity)
            {
                return;
            }

            if (item.ReturnSellerId != item.ProductSellerId)
            {
                context.SkipAmbiguous($"Return {item.ReturnRequestId} references a product outside the return seller.");
                continue;
            }

            if (!context.ShouldScan(item.RequestedAtUtc))
            {
                continue;
            }

            context.MarkScanned();
            var exists = await dbContext.InventoryMovements.AnyAsync(
                movement => movement.MovementType == InventoryMovementType.ReturnRequested
                    && movement.ReturnRequestId == item.ReturnRequestId
                    && movement.ProductVariantId == item.ProductVariantId,
                cancellationToken);
            if (exists)
            {
                context.SkipExisting();
                continue;
            }

            context.AddMovement(new InventoryMovement(
                item.ReturnSellerId,
                item.ProductId,
                item.ProductVariantId,
                InventoryMovementType.ReturnRequested,
                item.StockQuantity,
                item.StockQuantity,
                item.ReservedQuantity,
                item.ReservedQuantity,
                item.VariantStatus,
                item.VariantStatus,
                BackfillSource,
                "Historical backfill reconstructed return-request context; stock and reserved quantities were not changed automatically.",
                actorUserId: null,
                batchReference: null,
                item.RequestedAtUtc,
                orderId: item.OrderId,
                returnRequestId: item.ReturnRequestId));
            if (!context.DryRun)
            {
                dbContext.InventoryMovements.Add(context.LastMovement!);
            }
        }
    }

    private static async Task BackfillRefundsAsync(
        BackfillContext context,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!context.HasCapacity)
        {
            return;
        }

        var refunds = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.Status == RefundStatus.Refunded && refund.RefundedAtUtc.HasValue)
            .Where(refund => !context.SellerId.HasValue || refund.SellerId == context.SellerId.Value)
            .OrderBy(refund => refund.RefundedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var refund in refunds)
        {
            if (!context.HasCapacity)
            {
                return;
            }

            if (!context.ShouldScan(refund.RefundedAtUtc!.Value))
            {
                continue;
            }

            var variantIds = refund.ReturnRequestId.HasValue
                ? await dbContext.ReturnItems
                    .AsNoTracking()
                    .Where(item => item.ReturnRequestId == refund.ReturnRequestId.Value)
                    .Select(item => item.ProductVariantId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                : await dbContext.OrderItems
                    .AsNoTracking()
                    .Where(item => item.OrderId == refund.OrderId)
                    .Select(item => item.ProductVariantId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            if (variantIds.Count == 0)
            {
                context.SkipAmbiguous($"Refund {refund.Id} has no derivable order or return item variants.");
                continue;
            }

            var snapshots = await dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => variantIds.Contains(variant.Id))
                .Join(
                    dbContext.Products.AsNoTracking(),
                    variant => variant.ProductId,
                    product => product.Id,
                    (variant, product) => new RefundVariantSnapshot(
                        product.SellerId,
                        product.Id,
                        variant.Id,
                        variant.StockQuantity,
                        variant.ReservedQuantity,
                        variant.Status))
                .ToListAsync(cancellationToken);

            foreach (var snapshot in snapshots)
            {
                if (!context.HasCapacity)
                {
                    return;
                }

                context.MarkScanned();
                if (snapshot.SellerId != refund.SellerId)
                {
                    context.SkipAmbiguous($"Refund {refund.Id} references a product outside the refund seller.");
                    continue;
                }

                var exists = await dbContext.InventoryMovements.AnyAsync(
                    movement => movement.MovementType == InventoryMovementType.RefundCompleted
                        && movement.RefundId == refund.Id
                        && movement.ProductVariantId == snapshot.ProductVariantId,
                    cancellationToken);
                if (exists)
                {
                    context.SkipExisting();
                    continue;
                }

                context.AddMovement(new InventoryMovement(
                    refund.SellerId,
                    snapshot.ProductId,
                    snapshot.ProductVariantId,
                    InventoryMovementType.RefundCompleted,
                    snapshot.StockQuantity,
                    snapshot.StockQuantity,
                    snapshot.ReservedQuantity,
                    snapshot.ReservedQuantity,
                    snapshot.Status,
                    snapshot.Status,
                    BackfillSource,
                    "Historical backfill reconstructed refund-completed context; stock and reserved quantities were not changed automatically.",
                    refund.ApprovedByUserId,
                    batchReference: null,
                    refund.RefundedAtUtc.Value,
                    orderId: refund.OrderId,
                    paymentId: refund.PaymentId,
                    returnRequestId: refund.ReturnRequestId,
                    refundId: refund.Id));
                if (!context.DryRun)
                {
                    dbContext.InventoryMovements.Add(context.LastMovement!);
                }
            }
        }
    }

    private static Order? FindOrderForCart(
        IReadOnlyDictionary<Guid, Order[]> ordersByCartId,
        Guid cartId) =>
        ordersByCartId.TryGetValue(cartId, out var orders) ? orders[0] : null;

    private static Payment? FindPaymentForOrder(
        IReadOnlyDictionary<Guid, Payment[]> paymentsByOrderId,
        Guid orderId,
        params PaymentStatus[] statuses) =>
        paymentsByOrderId.TryGetValue(orderId, out var payments)
            ? payments.FirstOrDefault(payment => statuses.Contains(payment.Status))
            : null;

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private sealed class BackfillContext(
        bool dryRun,
        Guid? sellerId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int batchSize)
    {
        private readonly List<string> _warnings = [];

        public bool DryRun { get; } = dryRun;

        public Guid? SellerId { get; } = sellerId;

        public bool HasCapacity => ScannedCount < batchSize;

        public int ScannedCount { get; private set; }

        public int CreatedMovementCount { get; private set; }

        public int SkippedExistingCount { get; private set; }

        public int SkippedAmbiguousCount { get; private set; }

        public InventoryMovement? LastMovement { get; private set; }

        public bool ShouldScan(DateTimeOffset occurredAtUtc) =>
            (!fromUtc.HasValue || occurredAtUtc >= fromUtc.Value)
            && (!toUtc.HasValue || occurredAtUtc <= toUtc.Value)
            && HasCapacity;

        public void MarkScanned() => ScannedCount++;

        public void SkipExisting() => SkippedExistingCount++;

        public void SkipAmbiguous(string warning)
        {
            SkippedAmbiguousCount++;
            if (_warnings.Count < 25)
            {
                _warnings.Add(warning);
            }
        }

        public void AddMovement(InventoryMovement movement)
        {
            LastMovement = movement;
            CreatedMovementCount++;
        }

        public AdminInventoryLedgerBackfillResponse ToResponse() =>
            new(DryRun, ScannedCount, CreatedMovementCount, SkippedExistingCount, SkippedAmbiguousCount, _warnings);
    }

    private sealed record ReservationBackfillProjection(
        Guid ReservationId,
        Guid CartId,
        Guid ProductVariantId,
        int Quantity,
        InventoryReservationStatus Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ConfirmedAtUtc,
        DateTimeOffset? ExpiredAtUtc,
        DateTimeOffset? CancelledAtUtc,
        Guid SellerId,
        Guid ProductId,
        int StockQuantity,
        ProductVariantStatus VariantStatus);

    private sealed record ReturnBackfillProjection(
        Guid ReturnRequestId,
        Guid OrderId,
        Guid ReturnSellerId,
        DateTimeOffset RequestedAtUtc,
        Guid ProductId,
        Guid ProductVariantId,
        int StockQuantity,
        int ReservedQuantity,
        ProductVariantStatus VariantStatus,
        Guid ProductSellerId);

    private sealed record RefundVariantSnapshot(
        Guid SellerId,
        Guid ProductId,
        Guid ProductVariantId,
        int StockQuantity,
        int ReservedQuantity,
        ProductVariantStatus Status);
}

public sealed record AdminInventoryLedgerBackfillRequest(
    bool DryRun = true,
    Guid? SellerId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int? BatchSize = null);

public sealed record AdminInventoryLedgerBackfillResponse(
    bool DryRun,
    int ScannedCount,
    int CreatedMovementCount,
    int SkippedExistingCount,
    int SkippedAmbiguousCount,
    IReadOnlyCollection<string> Warnings);
