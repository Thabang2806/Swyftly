using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Inventory;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Inventory;

public sealed class EfInventoryReservationService(MabuntleDbContext dbContext) : IInventoryReservationService
{
    public async Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ReserveCartAsync(
        ReserveCartInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BuyerId == Guid.Empty)
        {
            return Validation("buyerId", "Buyer id is required.");
        }

        if (request.CartId == Guid.Empty)
        {
            return Validation("cartId", "Cart id is required.");
        }

        if (request.ReservationDuration <= TimeSpan.Zero)
        {
            return Validation("reservationDuration", "Reservation duration must be positive.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var useDatabaseConditionalUpdates = dbContext.Database.IsRelational();
        var cart = await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.Id == request.CartId
                    && cart.BuyerId == request.BuyerId
                    && cart.Status == CartStatus.Active,
                cancellationToken);
        if (cart is null)
        {
            return Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(
                Error.NotFound("InventoryReservations.CartNotFound", "Active cart was not found."));
        }

        if (cart.Items.Count == 0)
        {
            return Validation("cart", "Cart must contain at least one item before inventory can be reserved.");
        }

        var existingActiveReservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == cart.Id && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);

        var variants = new Dictionary<Guid, ProductVariant>();
        foreach (var item in cart.Items)
        {
            var variant = await LoadVariantAsync(
                item.ProductVariantId,
                trackChanges: !useDatabaseConditionalUpdates,
                cancellationToken);
            if (variant is null || variant.Status != ProductVariantStatus.Active)
            {
                return Validation("cart", $"Product variant {item.ProductVariantId} is not available.");
            }

            var existingQuantity = existingActiveReservations
                .Where(reservation => reservation.ProductVariantId == item.ProductVariantId)
                .Sum(reservation => reservation.Quantity);
            if (item.Quantity > variant.AvailableQuantity + existingQuantity)
            {
                return Validation("cart", $"Insufficient stock for product variant {item.ProductVariantId}.");
            }

            variants[variant.Id] = variant;
        }

        foreach (var reservation in existingActiveReservations)
        {
            var beforeRelease = await InventoryMovementRecorder.LoadSnapshotAsync(
                dbContext,
                reservation.ProductVariantId,
                cancellationToken);
            var released = await TryReleaseVariantReservationAsync(
                reservation.ProductVariantId,
                reservation.Quantity,
                useDatabaseConditionalUpdates,
                cancellationToken);
            if (!released)
            {
                return Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(
                    Error.Conflict(
                        "InventoryReservations.ReleaseConflict",
                        $"Reserved stock could not be released for product variant {reservation.ProductVariantId}."));
            }

            reservation.Cancel(request.StartedAtUtc);
            if (beforeRelease is not null)
            {
                var afterRelease = beforeRelease with
                {
                    ReservedQuantity = beforeRelease.ReservedQuantity - reservation.Quantity
                };
                dbContext.InventoryMovements.Add(InventoryMovementRecorder.Create(
                    beforeRelease,
                    afterRelease,
                    InventoryMovementType.ReservationReleased,
                    "CheckoutReservationRefresh",
                    "Existing checkout reservation was released before refreshing cart stock holds.",
                    actorUserId: null,
                    batchReference: null,
                    occurredAtUtc: request.StartedAtUtc,
                    cartId: cart.Id,
                    reservationId: reservation.Id));
            }
        }

        var expiresAtUtc = request.StartedAtUtc.Add(request.ReservationDuration);
        var results = new List<InventoryReservationResult>();

        foreach (var item in cart.Items)
        {
            var variant = variants[item.ProductVariantId];
            var beforeReserve = await InventoryMovementRecorder.LoadSnapshotAsync(
                dbContext,
                variant.Id,
                cancellationToken);
            var reserved = await TryReserveVariantAsync(
                variant.Id,
                item.Quantity,
                useDatabaseConditionalUpdates,
                cancellationToken);
            if (!reserved)
            {
                return Validation("cart", $"Insufficient stock for product variant {item.ProductVariantId}.");
            }

            var reservation = new InventoryReservation(
                variant.Id,
                cart.BuyerId,
                cart.Id,
                item.Quantity,
                expiresAtUtc,
                request.StartedAtUtc);
            dbContext.InventoryReservations.Add(reservation);
            if (beforeReserve is not null)
            {
                var afterReserve = beforeReserve with
                {
                    ReservedQuantity = beforeReserve.ReservedQuantity + item.Quantity
                };
                dbContext.InventoryMovements.Add(InventoryMovementRecorder.Create(
                    beforeReserve,
                    afterReserve,
                    InventoryMovementType.ReservationCreated,
                    "CheckoutReservation",
                    "Cart checkout reserved stock while payment is pending.",
                    actorUserId: null,
                    batchReference: null,
                    occurredAtUtc: request.StartedAtUtc,
                    cartId: cart.Id,
                    reservationId: reservation.Id));
            }
            results.Add(Map(reservation));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<IReadOnlyCollection<InventoryReservationResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ExpireReservationsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var useDatabaseConditionalUpdates = dbContext.Database.IsRelational();
        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.Status == InventoryReservationStatus.Active
                && reservation.ExpiresAtUtc <= utcNow)
            .OrderBy(reservation => reservation.ExpiresAtUtc)
            .ToListAsync(cancellationToken);
        var results = new List<InventoryReservationResult>();

        foreach (var reservation in reservations)
        {
            var beforeRelease = await InventoryMovementRecorder.LoadSnapshotAsync(
                dbContext,
                reservation.ProductVariantId,
                cancellationToken);
            var released = await TryReleaseVariantReservationAsync(
                reservation.ProductVariantId,
                reservation.Quantity,
                useDatabaseConditionalUpdates,
                cancellationToken);
            if (!released)
            {
                return Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(
                    Error.Conflict(
                        "InventoryReservations.ReleaseConflict",
                        $"Reserved stock could not be released for product variant {reservation.ProductVariantId}."));
            }

            reservation.Expire(utcNow);
            if (beforeRelease is not null)
            {
                var afterRelease = beforeRelease with
                {
                    ReservedQuantity = beforeRelease.ReservedQuantity - reservation.Quantity
                };
                dbContext.InventoryMovements.Add(InventoryMovementRecorder.Create(
                    beforeRelease,
                    afterRelease,
                    InventoryMovementType.ReservationExpired,
                    "ReservationExpiryWorker",
                    "Checkout reservation expired before payment confirmation.",
                    actorUserId: null,
                    batchReference: null,
                    occurredAtUtc: utcNow,
                    cartId: reservation.CartId,
                    reservationId: reservation.Id));
            }
            results.Add(Map(reservation));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<IReadOnlyCollection<InventoryReservationResult>>.Success(results);
    }

    private static InventoryReservationResult Map(InventoryReservation reservation) =>
        new(
            reservation.Id,
            reservation.ProductVariantId,
            reservation.BuyerId,
            reservation.CartId,
            reservation.Quantity,
            reservation.Status.ToString(),
            reservation.ExpiresAtUtc);

    private async Task<ProductVariant?> LoadVariantAsync(
        Guid productVariantId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProductVariants.AsQueryable();
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
            variant => variant.Id == productVariantId,
            cancellationToken);
    }

    private async Task<bool> TryReserveVariantAsync(
        Guid productVariantId,
        int quantity,
        bool useDatabaseConditionalUpdates,
        CancellationToken cancellationToken)
    {
        if (useDatabaseConditionalUpdates)
        {
            var updatedRows = await dbContext.ProductVariants
                .Where(variant => variant.Id == productVariantId
                    && variant.Status == ProductVariantStatus.Active
                    && variant.StockQuantity - variant.ReservedQuantity >= quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        variant => variant.ReservedQuantity,
                        variant => variant.ReservedQuantity + quantity),
                    cancellationToken);

            return updatedRows == 1;
        }

        var variant = await LoadVariantAsync(productVariantId, trackChanges: true, cancellationToken);
        if (variant is null || variant.Status != ProductVariantStatus.Active || variant.AvailableQuantity < quantity)
        {
            return false;
        }

        variant.Reserve(quantity);
        return true;
    }

    private async Task<bool> TryReleaseVariantReservationAsync(
        Guid productVariantId,
        int quantity,
        bool useDatabaseConditionalUpdates,
        CancellationToken cancellationToken)
    {
        if (useDatabaseConditionalUpdates)
        {
            var updatedRows = await dbContext.ProductVariants
                .Where(variant => variant.Id == productVariantId
                    && variant.ReservedQuantity >= quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        variant => variant.ReservedQuantity,
                        variant => variant.ReservedQuantity - quantity),
                    cancellationToken);

            return updatedRows == 1;
        }

        var variant = await LoadVariantAsync(productVariantId, trackChanges: true, cancellationToken);
        if (variant is null || variant.ReservedQuantity < quantity)
        {
            return false;
        }

        variant.ReleaseReservation(quantity);
        return true;
    }

    private static Result<IReadOnlyCollection<InventoryReservationResult>> Validation(string propertyName, string message) =>
        Result<IReadOnlyCollection<InventoryReservationResult>>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));
}
