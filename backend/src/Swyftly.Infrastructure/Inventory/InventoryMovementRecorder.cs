using Microsoft.EntityFrameworkCore;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Inventory;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Inventory;

internal static class InventoryMovementRecorder
{
    internal sealed record VariantSnapshot(
        Guid SellerId,
        Guid ProductId,
        Guid ProductVariantId,
        int StockQuantity,
        int ReservedQuantity,
        ProductVariantStatus Status);

    public static async Task<VariantSnapshot?> LoadSnapshotAsync(
        SwyftlyDbContext dbContext,
        Guid productVariantId,
        CancellationToken cancellationToken) =>
        await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.Id == productVariantId)
            .Join(
                dbContext.Products.AsNoTracking(),
                variant => variant.ProductId,
                product => product.Id,
                (variant, product) => new VariantSnapshot(
                    product.SellerId,
                    product.Id,
                    variant.Id,
                    variant.StockQuantity,
                    variant.ReservedQuantity,
                    variant.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public static InventoryMovement Create(
        VariantSnapshot before,
        VariantSnapshot after,
        InventoryMovementType movementType,
        string source,
        string reason,
        Guid? actorUserId,
        string? batchReference,
        DateTimeOffset occurredAtUtc,
        Guid? cartId = null,
        Guid? orderId = null,
        Guid? reservationId = null,
        Guid? paymentId = null,
        Guid? returnRequestId = null,
        Guid? refundId = null)
    {
        if (before.ProductVariantId != after.ProductVariantId
            || before.ProductId != after.ProductId
            || before.SellerId != after.SellerId)
        {
            throw new InvalidOperationException("Inventory movement snapshots must refer to the same seller product variant.");
        }

        return new InventoryMovement(
            before.SellerId,
            before.ProductId,
            before.ProductVariantId,
            movementType,
            before.StockQuantity,
            after.StockQuantity,
            before.ReservedQuantity,
            after.ReservedQuantity,
            before.Status,
            after.Status,
            source,
            reason,
            actorUserId,
            batchReference,
            occurredAtUtc,
            cartId,
            orderId,
            reservationId,
            paymentId,
            returnRequestId,
            refundId);
    }

    public static InventoryMovement CreateContext(
        VariantSnapshot snapshot,
        InventoryMovementType movementType,
        string source,
        string reason,
        Guid? actorUserId,
        string? batchReference,
        DateTimeOffset occurredAtUtc,
        Guid? cartId = null,
        Guid? orderId = null,
        Guid? reservationId = null,
        Guid? paymentId = null,
        Guid? returnRequestId = null,
        Guid? refundId = null) =>
        Create(
            snapshot,
            snapshot,
            movementType,
            source,
            reason,
            actorUserId,
            batchReference,
            occurredAtUtc,
            cartId,
            orderId,
            reservationId,
            paymentId,
            returnRequestId,
            refundId);
}
