using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Inventory;

public sealed class InventoryReservation : Entity
{
    private InventoryReservation()
    {
    }

    public InventoryReservation(
        Guid productVariantId,
        Guid buyerId,
        Guid cartId,
        int quantity,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (productVariantId == Guid.Empty)
        {
            throw new ArgumentException("Product variant id is required.", nameof(productVariantId));
        }

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Buyer id is required.", nameof(buyerId));
        }

        if (cartId == Guid.Empty)
        {
            throw new ArgumentException("Cart id is required.", nameof(cartId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Reservation expiry must be in the future.", nameof(expiresAtUtc));
        }

        ProductVariantId = productVariantId;
        BuyerId = buyerId;
        CartId = cartId;
        Quantity = quantity;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        Status = InventoryReservationStatus.Active;
    }

    public Guid ProductVariantId { get; private set; }

    public Guid BuyerId { get; private set; }

    public Guid CartId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public InventoryReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public DateTimeOffset? ExpiredAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public bool IsActiveAt(DateTimeOffset utcNow) =>
        Status == InventoryReservationStatus.Active && ExpiresAtUtc > utcNow;

    public void Confirm(DateTimeOffset confirmedAtUtc)
    {
        EnsureActive();
        Status = InventoryReservationStatus.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    public void Expire(DateTimeOffset expiredAtUtc)
    {
        EnsureActive();
        Status = InventoryReservationStatus.Expired;
        ExpiredAtUtc = expiredAtUtc;
    }

    public void Cancel(DateTimeOffset cancelledAtUtc)
    {
        EnsureActive();
        Status = InventoryReservationStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    private void EnsureActive()
    {
        if (Status != InventoryReservationStatus.Active)
        {
            throw new InvalidOperationException("Only active reservations can change status.");
        }
    }
}
