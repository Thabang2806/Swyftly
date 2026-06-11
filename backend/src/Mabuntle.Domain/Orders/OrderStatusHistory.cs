using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Orders;

public sealed class OrderStatusHistory : Entity
{
    private OrderStatusHistory()
    {
    }

    public OrderStatusHistory(
        Guid orderId,
        OrderStatus? previousStatus,
        OrderStatus newStatus,
        DateTimeOffset changedAtUtc,
        string? reason = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        OrderId = orderId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAtUtc = changedAtUtc;
        Reason = TrimOrNull(reason);
    }

    public Guid OrderId { get; private set; }

    public OrderStatus? PreviousStatus { get; private set; }

    public OrderStatus NewStatus { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public string? Reason { get; private set; }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
