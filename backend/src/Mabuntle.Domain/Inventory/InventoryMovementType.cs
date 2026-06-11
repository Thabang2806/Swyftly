namespace Mabuntle.Domain.Inventory;

public enum InventoryMovementType
{
    SellerAdjustment = 0,
    BulkImportAdjustment = 1,
    ReservationCreated = 2,
    ReservationReleased = 3,
    ReservationExpired = 4,
    ReservationConfirmed = 5,
    PaymentFailedReservationReleased = 6,
    ReturnRequested = 7,
    RefundCompleted = 8,
    ReturnRestocked = 9
}
