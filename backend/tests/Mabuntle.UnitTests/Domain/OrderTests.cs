using Mabuntle.Domain.Orders;

namespace Mabuntle.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Constructor_StartsPendingPaymentAndCreatesStatusHistory()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), createdAt);

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        var history = Assert.Single(order.StatusHistory);
        Assert.Null(history.PreviousStatus);
        Assert.Equal(OrderStatus.PendingPayment, history.NewStatus);
        Assert.Equal(createdAt, history.ChangedAtUtc);
        Assert.Equal("OrderCreated", history.Reason);
    }

    [Fact]
    public void AddItem_CalculatesSubtotalAndTotal()
    {
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
            shippingAmount: 80m,
            platformFeeAmount: 25m,
            discountAmount: 30m);

        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Cotton Dress", "SKU-1", "M", "Black", 499m, 2);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Leather Shoes", "SKU-2", "8", "Brown", 799m, 1);

        Assert.Equal(1797m, order.ItemsSubtotal);
        Assert.Equal(1872m, order.TotalAmount);
    }

    [Fact]
    public void ChangeStatus_AddsStatusHistory()
    {
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"));
        var changedAt = DateTimeOffset.Parse("2026-05-18T12:05:00Z");

        order.ChangeStatus(OrderStatus.Paid, changedAt, "PaymentConfirmed");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(2, order.StatusHistory.Count);
        var paidHistory = order.StatusHistory.Last();
        Assert.Equal(OrderStatus.PendingPayment, paidHistory.PreviousStatus);
        Assert.Equal(OrderStatus.Paid, paidHistory.NewStatus);
        Assert.Equal(changedAt, paidHistory.ChangedAtUtc);
        Assert.Equal("PaymentConfirmed", paidHistory.Reason);
    }

    [Fact]
    public void Shipment_StartsAwaitingFulfilmentAndRecordsEvent()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-18T12:00:00Z");

        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), createdAt);

        Assert.Equal(ShipmentStatus.AwaitingFulfilment, shipment.Status);
        var shipmentEvent = Assert.Single(shipment.Events);
        Assert.Equal(ShipmentStatus.AwaitingFulfilment, shipmentEvent.Status);
        Assert.Equal("ShipmentCreated", shipmentEvent.EventType);
    }

    [Fact]
    public void Shipment_UpdateTracking_TrimsValuesAndRecordsEvent()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var occurredAt = DateTimeOffset.Parse("2026-05-18T12:05:00Z");

        shipment.UpdateTracking("  Courier  ", "  TRK-123  ", " https://tracking.example/TRK-123 ", null, occurredAt);

        Assert.Equal("Courier", shipment.CarrierName);
        Assert.Equal("TRK-123", shipment.TrackingNumber);
        Assert.Equal("https://tracking.example/TRK-123", shipment.TrackingUrl);
        Assert.Equal(2, shipment.Events.Count);
        Assert.Equal("TrackingUpdated", shipment.Events.Last().EventType);
    }

    [Fact]
    public void Shipment_MarkDelivered_RequiresInTransit()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered(DateTimeOffset.UtcNow));
    }
}
