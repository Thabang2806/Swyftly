using Swyftly.Application.Common.Results;

namespace Swyftly.Application.Orders;

public interface IOrderFulfillmentService
{
    Task<Result<OrderResult>> MarkProcessingAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> AddTrackingAsync(
        AddOrderTrackingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> MarkReadyToShipAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> MarkShippedAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> MarkDeliveredAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> MarkDeliveryFailedAsync(
        OrderFulfillmentExceptionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> MarkReturnedToSenderAsync(
        OrderFulfillmentExceptionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OrderFulfillmentRequest(
    Guid SellerId,
    Guid OrderId,
    DateTimeOffset OccurredAtUtc);

public sealed record AddOrderTrackingRequest(
    Guid SellerId,
    Guid OrderId,
    string CarrierName,
    string TrackingNumber,
    string? TrackingUrl,
    string? Note,
    DateTimeOffset OccurredAtUtc);

public sealed record OrderFulfillmentExceptionRequest(
    Guid SellerId,
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAtUtc);
