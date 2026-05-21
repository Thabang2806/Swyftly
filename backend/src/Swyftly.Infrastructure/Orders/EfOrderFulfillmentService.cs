using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Orders;
using Swyftly.Domain.Orders;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Orders;

public sealed class EfOrderFulfillmentService(SwyftlyDbContext dbContext) : IOrderFulfillmentService
{
    public async Task<Result<OrderResult>> MarkProcessingAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.SellerId, request.OrderId);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status is not (OrderStatus.Paid or OrderStatus.Processing))
        {
            return InvalidTransition("Only paid orders can be marked as processing.");
        }

        var statusChanged = order.Status != OrderStatus.Processing;
        if (statusChanged)
        {
            order.ChangeStatus(OrderStatus.Processing, request.OccurredAtUtc, "SellerMarkedProcessing");
            TrackLatestOrderStatusHistory(order);
        }

        EnsureShipment(order, request.OccurredAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> AddTrackingAsync(
        AddOrderTrackingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateTracking(request);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status is not (OrderStatus.Paid or OrderStatus.Processing or OrderStatus.ReadyToShip or OrderStatus.Shipped))
        {
            return InvalidTransition("Tracking can only be added to paid or fulfilment orders.");
        }

        var (shipment, isNew) = EnsureShipment(order, request.OccurredAtUtc);
        shipment.UpdateTracking(
            request.CarrierName,
            request.TrackingNumber,
            request.TrackingUrl,
            request.Note,
            request.OccurredAtUtc);

        if (!isNew)
        {
            TrackLatestShipmentEvent(shipment);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> MarkReadyToShipAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.SellerId, request.OrderId);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status is not (OrderStatus.Paid or OrderStatus.Processing or OrderStatus.ReadyToShip))
        {
            return InvalidTransition("Only paid or processing orders can be marked ready to ship.");
        }

        if (order.Status != OrderStatus.ReadyToShip)
        {
            order.ChangeStatus(OrderStatus.ReadyToShip, request.OccurredAtUtc, "SellerMarkedReadyToShip");
            TrackLatestOrderStatusHistory(order);
        }

        var (shipment, isNew) = EnsureShipment(order, request.OccurredAtUtc);
        if (shipment.Status != ShipmentStatus.ReadyForCourier)
        {
            try
            {
                shipment.MarkReadyForCourier(request.OccurredAtUtc);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidTransition(exception.Message);
            }

            if (!isNew)
            {
                TrackLatestShipmentEvent(shipment);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> MarkShippedAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.SellerId, request.OrderId);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status is not (OrderStatus.Paid or OrderStatus.Processing or OrderStatus.ReadyToShip or OrderStatus.Shipped))
        {
            return InvalidTransition("Only paid or fulfilment orders can be marked as shipped.");
        }

        var statusChanged = order.Status != OrderStatus.Shipped;
        if (statusChanged)
        {
            order.ChangeStatus(OrderStatus.Shipped, request.OccurredAtUtc, "SellerMarkedShipped");
            TrackLatestOrderStatusHistory(order);
        }

        var (shipment, isNew) = EnsureShipment(order, request.OccurredAtUtc);
        if (shipment.Status != ShipmentStatus.InTransit)
        {
            try
            {
                shipment.MarkInTransit(request.OccurredAtUtc);
            }
            catch (InvalidOperationException exception)
            {
                return InvalidTransition(exception.Message);
            }

            if (!isNew)
            {
                TrackLatestShipmentEvent(shipment);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> MarkDeliveredAsync(
        OrderFulfillmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.SellerId, request.OrderId);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        var shipment = order.Shipments
            .OrderByDescending(existing => existing.CreatedAtUtc)
            .FirstOrDefault();

        if (order.Status == OrderStatus.Delivered && shipment?.Status == ShipmentStatus.Delivered)
        {
            return Result<OrderResult>.Success(Map(order));
        }

        if (order.Status != OrderStatus.Shipped)
        {
            return InvalidTransition("Only shipped orders can be marked as delivered.");
        }

        if (shipment is null || shipment.Status != ShipmentStatus.InTransit)
        {
            return InvalidTransition("Only in-transit shipments can be marked as delivered.");
        }

        order.ChangeStatus(OrderStatus.Delivered, request.OccurredAtUtc, "SellerMarkedDelivered");
        TrackLatestOrderStatusHistory(order);
        shipment.MarkDelivered(request.OccurredAtUtc);
        TrackLatestShipmentEvent(shipment);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> MarkDeliveryFailedAsync(
        OrderFulfillmentExceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateException(request);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.Shipped)
        {
            return InvalidTransition("Only shipped orders can have delivery failure recorded.");
        }

        var shipment = LatestShipment(order);
        if (shipment is null)
        {
            return InvalidTransition("Only orders with an active shipment can have delivery failure recorded.");
        }

        if (shipment.Status == ShipmentStatus.DeliveryFailed)
        {
            return Result<OrderResult>.Success(Map(order));
        }

        try
        {
            shipment.MarkDeliveryFailed(request.Reason, request.OccurredAtUtc);
        }
        catch (ArgumentException exception)
        {
            return Result<OrderResult>.Failure(Error.Validation([
                new ValidationFailure(ToCamelCase(exception.ParamName ?? "reason"), exception.Message)
            ]));
        }
        catch (InvalidOperationException exception)
        {
            return InvalidTransition(exception.Message);
        }

        TrackLatestShipmentEvent(shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    public async Task<Result<OrderResult>> MarkReturnedToSenderAsync(
        OrderFulfillmentExceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateException(request);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.Shipped)
        {
            return InvalidTransition("Only shipped orders can have return-to-sender recorded.");
        }

        var shipment = LatestShipment(order);
        if (shipment is null)
        {
            return InvalidTransition("Only orders with an active shipment can have return-to-sender recorded.");
        }

        if (shipment.Status == ShipmentStatus.ReturnedToSender)
        {
            return Result<OrderResult>.Success(Map(order));
        }

        try
        {
            shipment.MarkReturnedToSender(request.Reason, request.OccurredAtUtc);
        }
        catch (ArgumentException exception)
        {
            return Result<OrderResult>.Failure(Error.Validation([
                new ValidationFailure(ToCamelCase(exception.ParamName ?? "reason"), exception.Message)
            ]));
        }
        catch (InvalidOperationException exception)
        {
            return InvalidTransition(exception.Message);
        }

        TrackLatestShipmentEvent(shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    private async Task<Order?> GetSellerOrderAsync(
        Guid sellerId,
        Guid orderId,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .SingleOrDefaultAsync(
                order => order.Id == orderId && order.SellerId == sellerId,
                cancellationToken);

    private static Shipment? LatestShipment(Order order) =>
        order.Shipments
            .OrderByDescending(existing => existing.CreatedAtUtc)
            .FirstOrDefault();

    private (Shipment Shipment, bool IsNew) EnsureShipment(Order order, DateTimeOffset createdAtUtc)
    {
        var shipment = order.Shipments
            .OrderByDescending(existing => existing.CreatedAtUtc)
            .FirstOrDefault();
        if (shipment is not null)
        {
            return (shipment, false);
        }

        shipment = new Shipment(order.Id, order.SellerId, order.BuyerId, createdAtUtc);
        dbContext.Shipments.Add(shipment);
        return (shipment, true);
    }

    private static Result<OrderResult>? Validate(Guid sellerId, Guid orderId)
    {
        var failures = new List<ValidationFailure>();

        if (sellerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("sellerId", "Seller id is required."));
        }

        if (orderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        return failures.Count == 0
            ? null
            : Result<OrderResult>.Failure(Error.Validation(failures));
    }

    private static Result<OrderResult>? ValidateTracking(AddOrderTrackingRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.SellerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("sellerId", "Seller id is required."));
        }

        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (string.IsNullOrWhiteSpace(request.CarrierName))
        {
            failures.Add(new ValidationFailure("carrierName", "Carrier name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.TrackingNumber))
        {
            failures.Add(new ValidationFailure("trackingNumber", "Tracking number is required."));
        }

        return failures.Count == 0
            ? null
            : Result<OrderResult>.Failure(Error.Validation(failures));
    }

    private static Result<OrderResult>? ValidateException(OrderFulfillmentExceptionRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.SellerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("sellerId", "Seller id is required."));
        }

        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            failures.Add(new ValidationFailure("reason", "Reason is required."));
        }
        else if (request.Reason.Trim().Length > Shipment.ExceptionReasonMaxLength)
        {
            failures.Add(new ValidationFailure("reason", $"Reason must be {Shipment.ExceptionReasonMaxLength} characters or fewer."));
        }

        return failures.Count == 0
            ? null
            : Result<OrderResult>.Failure(Error.Validation(failures));
    }

    private static Result<OrderResult> OrderNotFound() =>
        Result<OrderResult>.Failure(
            Error.NotFound("Orders.NotFound", "Order was not found."));

    private static Result<OrderResult> InvalidTransition(string description) =>
        Result<OrderResult>.Failure(
            Error.Conflict("Orders.InvalidFulfillmentTransition", description));

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private void TrackLatestShipmentEvent(Shipment shipment)
    {
        var shipmentEvent = shipment.Events.LastOrDefault();
        if (shipmentEvent is not null)
        {
            dbContext.ShipmentEvents.Add(shipmentEvent);
        }
    }

    public static OrderResult Map(Order order) =>
        new(
            order.Id,
            order.BuyerId,
            order.SellerId,
            order.CartId,
            order.Status.ToString(),
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResult(
                    item.Id,
                    item.ProductId,
                    item.ProductVariantId,
                    item.ProductTitle,
                    item.Sku,
                    item.Size,
                    item.Colour,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal))
                .ToArray(),
            order.ItemsSubtotal,
            order.ShippingAmount,
            order.PlatformFeeAmount,
            order.DiscountAmount,
            order.TotalAmount,
            MapDeliveryAddress(order.DeliveryAddress),
            order.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .Select(history => new OrderStatusHistoryResult(
                    history.Id,
                    history.PreviousStatus?.ToString(),
                    history.NewStatus.ToString(),
                    history.ChangedAtUtc,
                    history.Reason))
                .ToArray(),
            order.Shipments
                .OrderBy(shipment => shipment.CreatedAtUtc)
                .Select(shipment => new ShipmentResult(
                    shipment.Id,
                    shipment.Status.ToString(),
                    shipment.CarrierName,
                    shipment.TrackingNumber,
                    shipment.TrackingUrl,
                    shipment.ShippedAtUtc,
                    shipment.DeliveredAtUtc,
                    shipment.Events
                        .OrderBy(shipmentEvent => shipmentEvent.OccurredAtUtc)
                        .Select(shipmentEvent => new ShipmentEventResult(
                            shipmentEvent.Id,
                            shipmentEvent.Status.ToString(),
                            shipmentEvent.EventType,
                            shipmentEvent.Message,
                            shipmentEvent.CarrierName,
                            shipmentEvent.TrackingNumber,
                            shipmentEvent.OccurredAtUtc))
                        .ToArray()))
                .ToArray(),
            order.DeliveryMethodId,
            order.DeliveryMethodName,
            order.DeliveryMethodType,
            order.DeliveryEstimatedMinDays,
            order.DeliveryEstimatedMaxDays);

    private static OrderDeliveryAddressResult? MapDeliveryAddress(OrderDeliveryAddress? address) =>
        address is null
            ? null
            : new OrderDeliveryAddressResult(
                address.RecipientName,
                address.PhoneNumber,
                address.AddressLine1,
                address.AddressLine2,
                address.Suburb,
                address.City,
                address.Province,
                address.PostalCode,
                address.CountryCode,
                address.DeliveryInstructions);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
