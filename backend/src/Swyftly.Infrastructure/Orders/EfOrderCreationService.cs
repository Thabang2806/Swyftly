using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Common.Validation;
using Swyftly.Application.Inventory;
using Swyftly.Application.Orders;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Infrastructure.Persistence;

namespace Swyftly.Infrastructure.Orders;

public sealed class EfOrderCreationService(
    SwyftlyDbContext dbContext,
    IInventoryReservationService inventoryReservationService) : IOrderCreationService
{
    public async Task<Result<OrderResult>> CreateFromCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailures = Validate(request);
        if (validationFailures.Count > 0)
        {
            return Result<OrderResult>.Failure(Error.Validation(validationFailures));
        }

        var cart = await GetCartAsync(request, cancellationToken);
        if (cart is null)
        {
            return Result<OrderResult>.Failure(
                Error.NotFound("Orders.CartNotFound", "Active cart was not found."));
        }

        if (cart.Items.Count == 0)
        {
            return Validation("cart", "Cart must contain at least one item before an order can be created.");
        }

        if (!cart.SellerId.HasValue)
        {
            return Validation("cart", "Cart must be associated with a seller before an order can be created.");
        }

        var deliveryAddressResult = await ResolveDeliveryAddressAsync(request, cancellationToken);
        if (deliveryAddressResult.IsFailure)
        {
            return Result<OrderResult>.Failure(deliveryAddressResult.Error);
        }

        var deliveryMethodResult = await ResolveDeliveryMethodAsync(
            cart.SellerId.Value,
            request.DeliveryMethodId,
            deliveryAddressResult.Value,
            cart.Subtotal,
            cancellationToken);
        if (deliveryMethodResult.IsFailure)
        {
            return Result<OrderResult>.Failure(deliveryMethodResult.Error);
        }

        var existingOrder = await dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .SingleOrDefaultAsync(
                order => order.CartId == cart.Id
                    && order.BuyerId == request.BuyerId
                    && order.Status == OrderStatus.PendingPayment,
                cancellationToken);
        if (existingOrder is not null)
        {
            var changed = false;
            if (existingOrder.DeliveryAddress is null)
            {
                existingOrder.SetDeliveryAddress(deliveryAddressResult.Value);
                changed = true;
            }

            if (existingOrder.DeliveryMethodId is null)
            {
                existingOrder.SetDeliveryMethod(
                    deliveryMethodResult.Value.DeliveryMethod,
                    deliveryMethodResult.Value.ShippingAmount);
                changed = true;
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result<OrderResult>.Success(Map(existingOrder));
        }

        var reservationResult = await inventoryReservationService.ReserveCartAsync(
            new ReserveCartInventoryRequest(
                request.BuyerId,
                cart.Id,
                request.StartedAtUtc,
                request.ReservationDuration),
            cancellationToken);
        if (reservationResult.IsFailure)
        {
            return Result<OrderResult>.Failure(reservationResult.Error);
        }

        var order = new Order(
            cart.BuyerId,
            cart.SellerId.Value,
            cart.Id,
            request.StartedAtUtc,
            deliveryMethodResult.Value.ShippingAmount,
            request.PlatformFeeAmount,
            request.DiscountAmount,
            deliveryAddressResult.Value);
        order.SetDeliveryMethod(
            deliveryMethodResult.Value.DeliveryMethod,
            deliveryMethodResult.Value.ShippingAmount);

        foreach (var item in cart.Items.OrderBy(item => item.CreatedAtUtc))
        {
            order.AddItem(
                item.ProductId,
                item.ProductVariantId,
                item.ProductTitle,
                item.Sku,
                item.Size,
                item.Colour,
                item.UnitPrice,
                item.Quantity);
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    private async Task<Cart?> GetCartAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Carts
            .Include(cart => cart.Items)
            .Where(cart => cart.BuyerId == request.BuyerId && cart.Status == CartStatus.Active);

        return request.CartId.HasValue
            ? await query.SingleOrDefaultAsync(cart => cart.Id == request.CartId.Value, cancellationToken)
            : await query.SingleOrDefaultAsync(cancellationToken);
    }

    private static List<ValidationFailure> Validate(CreateOrderFromCartRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.BuyerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerId", "Buyer id is required."));
        }

        if (request.CartId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("cartId", "Cart id cannot be empty."));
        }

        if (request.ReservationDuration <= TimeSpan.Zero)
        {
            failures.Add(new ValidationFailure("reservationDuration", "Reservation duration must be positive."));
        }

        if (request.ShippingAmount < 0)
        {
            failures.Add(new ValidationFailure("shippingAmount", "Shipping amount cannot be negative."));
        }

        if (request.PlatformFeeAmount < 0)
        {
            failures.Add(new ValidationFailure("platformFeeAmount", "Platform fee amount cannot be negative."));
        }

        if (request.DiscountAmount < 0)
        {
            failures.Add(new ValidationFailure("discountAmount", "Discount amount cannot be negative."));
        }

        if (request.DeliveryAddressId.HasValue && request.DeliveryAddressId.Value == Guid.Empty)
        {
            failures.Add(new ValidationFailure("deliveryAddressId", "Delivery address id cannot be empty."));
        }

        if (request.DeliveryAddressId.HasValue && request.DeliveryAddress is not null)
        {
            failures.Add(new ValidationFailure("deliveryAddress", "Provide either a saved delivery address id or an inline delivery address, not both."));
        }

        if (!request.DeliveryAddressId.HasValue && request.DeliveryAddress is null)
        {
            failures.Add(new ValidationFailure("deliveryAddress", "A delivery address is required."));
        }

        if (request.DeliveryAddress is not null)
        {
            try
            {
                _ = ToDeliveryAddress(request.BuyerId, request.DeliveryAddress);
            }
            catch (ArgumentException exception)
            {
                failures.Add(new ValidationFailure(ToCamelCase(exception.ParamName ?? "deliveryAddress"), exception.Message));
            }
        }

        if (!request.DeliveryMethodId.HasValue)
        {
            failures.Add(new ValidationFailure("deliveryMethodId", "A delivery method is required."));
        }
        else if (request.DeliveryMethodId.Value == Guid.Empty)
        {
            failures.Add(new ValidationFailure("deliveryMethodId", "Delivery method id cannot be empty."));
        }

        return failures;
    }

    private static Result<OrderResult> Validation(string propertyName, string message) =>
        Result<OrderResult>.Failure(Error.Validation([
            new ValidationFailure(propertyName, message)
        ]));

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

    private async Task<Result<ResolvedDeliveryMethod>> ResolveDeliveryMethodAsync(
        Guid sellerId,
        Guid? deliveryMethodId,
        OrderDeliveryAddress deliveryAddress,
        decimal cartSubtotal,
        CancellationToken cancellationToken)
    {
        if (!deliveryMethodId.HasValue)
        {
            return Result<ResolvedDeliveryMethod>.Failure(Error.Validation([
                new ValidationFailure("deliveryMethodId", "A delivery method is required.")
            ]));
        }

        var deliveryMethod = await dbContext.SellerDeliveryMethods
            .SingleOrDefaultAsync(
                method => method.Id == deliveryMethodId.Value && method.SellerId == sellerId,
                cancellationToken);
        if (deliveryMethod is null)
        {
            return Result<ResolvedDeliveryMethod>.Failure(
                Error.NotFound("Orders.DeliveryMethodNotFound", "Delivery method was not found."));
        }

        if (!deliveryMethod.IsActive)
        {
            return Result<ResolvedDeliveryMethod>.Failure(Error.Validation([
                new ValidationFailure("deliveryMethodId", "Delivery method is not active.")
            ]));
        }

        if (!deliveryMethod.MatchesAddress(deliveryAddress.CountryCode, deliveryAddress.Province))
        {
            return Result<ResolvedDeliveryMethod>.Failure(Error.Validation([
                new ValidationFailure("deliveryMethodId", "Delivery method does not serve the selected delivery address.")
            ]));
        }

        return Result<ResolvedDeliveryMethod>.Success(new ResolvedDeliveryMethod(
            deliveryMethod,
            deliveryMethod.CalculateShippingAmount(cartSubtotal)));
    }

    private async Task<Result<OrderDeliveryAddress>> ResolveDeliveryAddressAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DeliveryAddressId.HasValue)
        {
            var saved = await dbContext.BuyerDeliveryAddresses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    address => address.Id == request.DeliveryAddressId.Value
                        && address.BuyerId == request.BuyerId,
                    cancellationToken);

            return saved is null
                ? Result<OrderDeliveryAddress>.Failure(
                    Error.NotFound("Orders.DeliveryAddressNotFound", "Delivery address was not found."))
                : Result<OrderDeliveryAddress>.Success(ToDeliveryAddress(saved));
        }

        return request.DeliveryAddress is null
            ? Result<OrderDeliveryAddress>.Failure(Error.Validation([
                new ValidationFailure("deliveryAddress", "A delivery address is required.")
            ]))
            : Result<OrderDeliveryAddress>.Success(ToDeliveryAddress(request.BuyerId, request.DeliveryAddress));
    }

    private static OrderDeliveryAddress ToDeliveryAddress(BuyerDeliveryAddress address) =>
        new(
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

    private static OrderDeliveryAddress ToDeliveryAddress(
        Guid buyerId,
        OrderDeliveryAddressRequest request)
    {
        var address = new BuyerDeliveryAddress(
            buyerId,
            "Checkout",
            request.RecipientName,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.Suburb,
            request.City,
            request.Province,
            request.PostalCode,
            request.CountryCode,
            isDefault: false,
            request.DeliveryInstructions);

        return ToDeliveryAddress(address);
    }

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

    private sealed record ResolvedDeliveryMethod(
        SellerDeliveryMethod DeliveryMethod,
        decimal ShippingAmount);
}
