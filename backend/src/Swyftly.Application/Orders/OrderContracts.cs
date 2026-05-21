using Swyftly.Application.Common.Results;

namespace Swyftly.Application.Orders;

public interface IOrderCreationService
{
    Task<Result<OrderResult>> CreateFromCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateOrderFromCartRequest(
    Guid BuyerId,
    Guid? CartId,
    DateTimeOffset StartedAtUtc,
    TimeSpan ReservationDuration,
    decimal ShippingAmount = 0,
    decimal PlatformFeeAmount = 0,
    decimal DiscountAmount = 0,
    Guid? DeliveryAddressId = null,
    OrderDeliveryAddressRequest? DeliveryAddress = null,
    Guid? DeliveryMethodId = null);

public sealed record OrderDeliveryAddressRequest(
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null);

public sealed record OrderResult(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    Guid CartId,
    string Status,
    IReadOnlyCollection<OrderItemResult> Items,
    decimal ItemsSubtotal,
    decimal ShippingAmount,
    decimal PlatformFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    OrderDeliveryAddressResult? DeliveryAddress,
    IReadOnlyCollection<OrderStatusHistoryResult> StatusHistory,
    IReadOnlyCollection<ShipmentResult> Shipments,
    Guid? DeliveryMethodId = null,
    string? DeliveryMethodName = null,
    string? DeliveryMethodType = null,
    int? DeliveryEstimatedMinDays = null,
    int? DeliveryEstimatedMaxDays = null);

public sealed record OrderDeliveryAddressResult(
    string RecipientName,
    string PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    string? DeliveryInstructions = null);

public sealed record OrderItemResult(
    Guid OrderItemId,
    Guid ProductId,
    Guid ProductVariantId,
    string? ProductTitle,
    string Sku,
    string Size,
    string Colour,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderStatusHistoryResult(
    Guid StatusHistoryId,
    string? PreviousStatus,
    string NewStatus,
    DateTimeOffset ChangedAtUtc,
    string? Reason);

public sealed record ShipmentResult(
    Guid ShipmentId,
    string Status,
    string? CarrierName,
    string? TrackingNumber,
    string? TrackingUrl,
    DateTimeOffset? ShippedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    IReadOnlyCollection<ShipmentEventResult> Events);

public sealed record ShipmentEventResult(
    Guid ShipmentEventId,
    string Status,
    string EventType,
    string? Message,
    string? CarrierName,
    string? TrackingNumber,
    DateTimeOffset OccurredAtUtc);
