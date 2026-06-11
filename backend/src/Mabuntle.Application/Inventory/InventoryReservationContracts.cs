using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Inventory;

public interface IInventoryReservationService
{
    Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ReserveCartAsync(
        ReserveCartInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<InventoryReservationResult>>> ExpireReservationsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}

public sealed record ReserveCartInventoryRequest(
    Guid BuyerId,
    Guid CartId,
    DateTimeOffset StartedAtUtc,
    TimeSpan ReservationDuration);

public sealed record InventoryReservationResult(
    Guid ReservationId,
    Guid ProductVariantId,
    Guid BuyerId,
    Guid CartId,
    int Quantity,
    string Status,
    DateTimeOffset ExpiresAtUtc);
