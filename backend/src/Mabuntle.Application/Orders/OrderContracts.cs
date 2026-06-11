using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Sellers;

namespace Mabuntle.Application.Orders;

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
    Guid? DeliveryMethodId = null,
    Guid? PickupPointId = null);

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
    int? DeliveryEstimatedMaxDays = null,
    OrderPickupPointResult? PickupPoint = null,
    SellerPolicySnapshotResponse? SellerPolicySnapshot = null,
    OrderPaymentSummaryResult? PaymentSummary = null);

public sealed record OrderPaymentSummaryResult(
    Guid PaymentId,
    string ProviderName,
    string? ProviderReference,
    string Status,
    decimal Amount,
    string Currency,
    bool CheckoutUrlAvailable,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? FailedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset UpdatedAtUtc);

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
    string? DeliveryInstructions = null,
    string VerificationStatus = "Unverified",
    string? VerificationProvider = null,
    IReadOnlyCollection<string>? VerificationWarnings = null,
    DateTimeOffset? VerifiedAtUtc = null);

public sealed record OrderPickupPointResult(
    Guid PickupPointId,
    string ProviderName,
    string Code,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string? Suburb,
    string City,
    string Province,
    string PostalCode,
    string CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    string? OpeningHours);

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
    IReadOnlyCollection<ShipmentEventResult> Events,
    string? CarrierProviderName = null,
    string? CarrierServiceCode = null,
    string? ProviderShipmentReference = null,
    string? CarrierBookingStatus = null,
    string? ProviderStatus = null,
    string? ProviderLabelUrl = null,
    DateTimeOffset? ProviderLastSyncedAtUtc = null,
    string? ProviderError = null);

public sealed record ShipmentEventResult(
    Guid ShipmentEventId,
    string Status,
    string EventType,
    string? Message,
    string? CarrierName,
    string? TrackingNumber,
    DateTimeOffset OccurredAtUtc);
