using Mabuntle.Domain.Inventory;

namespace Mabuntle.UnitTests.Domain;

public class InventoryReservationTests
{
    [Fact]
    public void NewReservation_StartsActive()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var reservation = new InventoryReservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            createdAt.AddMinutes(15),
            createdAt);

        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);
        Assert.True(reservation.IsActiveAt(createdAt.AddMinutes(10)));
        Assert.False(reservation.IsActiveAt(createdAt.AddMinutes(15)));
    }

    [Fact]
    public void Reservation_RejectsInvalidQuantity()
    {
        var createdAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryReservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            createdAt.AddMinutes(15),
            createdAt));
    }

    [Fact]
    public void Expire_MovesActiveReservationToExpired()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var reservation = new InventoryReservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            createdAt.AddMinutes(15),
            createdAt);
        var expiredAt = createdAt.AddMinutes(16);

        reservation.Expire(expiredAt);

        Assert.Equal(InventoryReservationStatus.Expired, reservation.Status);
        Assert.Equal(expiredAt, reservation.ExpiredAtUtc);
    }

    [Fact]
    public void ConfirmedReservation_CannotBeExpired()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");
        var reservation = new InventoryReservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            createdAt.AddMinutes(15),
            createdAt);
        reservation.Confirm(createdAt.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() => reservation.Expire(createdAt.AddMinutes(16)));
    }
}
