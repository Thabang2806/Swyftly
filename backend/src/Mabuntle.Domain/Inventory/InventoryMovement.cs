using Mabuntle.Domain.Catalog;
using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Inventory;

public sealed class InventoryMovement : Entity
{
    public const int ReasonMaxLength = 1000;
    public const int SourceMaxLength = 80;
    public const int BatchReferenceMaxLength = 120;

    private InventoryMovement()
    {
    }

    public InventoryMovement(
        Guid sellerId,
        Guid productId,
        Guid productVariantId,
        InventoryMovementType movementType,
        int stockQuantityBefore,
        int stockQuantityAfter,
        int reservedQuantityBefore,
        int reservedQuantityAfter,
        ProductVariantStatus statusBefore,
        ProductVariantStatus statusAfter,
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
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (productVariantId == Guid.Empty)
        {
            throw new ArgumentException("Product variant id is required.", nameof(productVariantId));
        }

        SellerId = sellerId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        MovementType = movementType;
        StockQuantityBefore = stockQuantityBefore;
        StockQuantityAfter = stockQuantityAfter;
        ReservedQuantityBefore = reservedQuantityBefore;
        ReservedQuantityAfter = reservedQuantityAfter;
        QuantityDelta = stockQuantityAfter - stockQuantityBefore;
        StatusBefore = statusBefore;
        StatusAfter = statusAfter;
        Source = Required(source, nameof(source), SourceMaxLength);
        Reason = Required(reason, nameof(reason), ReasonMaxLength);
        ActorUserId = actorUserId;
        BatchReference = TrimOrNull(batchReference, BatchReferenceMaxLength);
        OccurredAtUtc = occurredAtUtc;
        CartId = OptionalId(cartId);
        OrderId = OptionalId(orderId);
        ReservationId = OptionalId(reservationId);
        PaymentId = OptionalId(paymentId);
        ReturnRequestId = OptionalId(returnRequestId);
        RefundId = OptionalId(refundId);
    }

    public Guid SellerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public InventoryMovementType MovementType { get; private set; }

    public int StockQuantityBefore { get; private set; }

    public int StockQuantityAfter { get; private set; }

    public int ReservedQuantityBefore { get; private set; }

    public int ReservedQuantityAfter { get; private set; }

    public int QuantityDelta { get; private set; }

    public ProductVariantStatus StatusBefore { get; private set; }

    public ProductVariantStatus StatusAfter { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public Guid? ActorUserId { get; private set; }

    public string? BatchReference { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? CartId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? ReservationId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public Guid? ReturnRequestId { get; private set; }

    public Guid? RefundId { get; private set; }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }

        return trimmed;
    }

    private static Guid? OptionalId(Guid? value) => value.HasValue && value.Value != Guid.Empty ? value : null;
}
