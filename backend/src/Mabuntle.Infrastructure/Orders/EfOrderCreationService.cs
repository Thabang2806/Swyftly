using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Analytics;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Delivery;
using Mabuntle.Application.Inventory;
using Mabuntle.Application.Orders;
using Mabuntle.Application.Sellers;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Delivery;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Sellers;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Orders;

public sealed class EfOrderCreationService(
    MabuntleDbContext dbContext,
    IInventoryReservationService inventoryReservationService,
    IAddressVerificationService addressVerificationService,
    IStorefrontAnalyticsService storefrontAnalyticsService,
    IBuyerGrowthOutcomeAttributionService buyerGrowthOutcomeAttributionService) : IOrderCreationService
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

        var existingPendingOrder = await GetExistingPendingOrderAsync(request, cancellationToken);
        if (existingPendingOrder is not null)
        {
            return Result<OrderResult>.Success(Map(existingPendingOrder));
        }

        var cart = await GetActiveCartAsync(request, cancellationToken);
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

        var pickupPointResult = await ResolvePickupPointAsync(
            deliveryMethodResult.Value.DeliveryMethod,
            request.PickupPointId,
            deliveryAddressResult.Value,
            cancellationToken);
        if (pickupPointResult.IsFailure)
        {
            return Result<OrderResult>.Failure(pickupPointResult.Error);
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

        var sellerPolicy = await dbContext.SellerStorePolicies
            .SingleOrDefaultAsync(policy => policy.SellerId == cart.SellerId.Value, cancellationToken);

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
        order.SetPickupPoint(pickupPointResult.Value);
        order.SetSellerPolicySnapshot(sellerPolicy, request.StartedAtUtc);

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
        cart.MarkCheckedOut();
        await dbContext.SaveChangesAsync(cancellationToken);
        await storefrontAnalyticsService.RecordOrderCreatedAsync(order.Id, cancellationToken);
        await buyerGrowthOutcomeAttributionService.RecordOrderCreatedAsync(order.Id, request.StartedAtUtc, cancellationToken);
        return Result<OrderResult>.Success(Map(order));
    }

    private async Task<Cart?> GetActiveCartAsync(
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

    private async Task<Order?> GetExistingPendingOrderAsync(
        CreateOrderFromCartRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.CartId.HasValue)
        {
            return null;
        }

        return await dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Events)
            .SingleOrDefaultAsync(
                order => order.CartId == request.CartId.Value
                    && order.BuyerId == request.BuyerId
                    && order.Status == OrderStatus.PendingPayment,
                cancellationToken);
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

        if (request.PickupPointId.HasValue && request.PickupPointId.Value == Guid.Empty)
        {
            failures.Add(new ValidationFailure("pickupPointId", "Pickup point id cannot be empty."));
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
                        .ToArray(),
                    shipment.CarrierProviderName,
                    shipment.CarrierServiceCode,
                    shipment.ProviderShipmentReference,
                    shipment.CarrierBookingStatus?.ToString(),
                    shipment.ProviderStatus,
                    shipment.ProviderLabelUrl,
                    shipment.ProviderLastSyncedAtUtc,
                    shipment.ProviderError))
                .ToArray(),
            order.DeliveryMethodId,
            order.DeliveryMethodName,
            order.DeliveryMethodType,
            order.DeliveryEstimatedMinDays,
            order.DeliveryEstimatedMaxDays,
            MapPickupPoint(order.PickupPoint),
            MapSellerPolicySnapshot(order.SellerPolicySnapshot));

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

    private async Task<Result<PickupPoint?>> ResolvePickupPointAsync(
        SellerDeliveryMethod deliveryMethod,
        Guid? pickupPointId,
        OrderDeliveryAddress deliveryAddress,
        CancellationToken cancellationToken)
    {
        if (deliveryMethod.MethodType != SellerDeliveryMethodType.PickupPoint)
        {
            return pickupPointId.HasValue
                ? Result<PickupPoint?>.Failure(Error.Validation([
                    new ValidationFailure("pickupPointId", "Pickup point can only be selected for pickup delivery methods.")
                ]))
                : Result<PickupPoint?>.Success(null);
        }

        if (!pickupPointId.HasValue)
        {
            return Result<PickupPoint?>.Failure(Error.Validation([
                new ValidationFailure("pickupPointId", "A pickup point is required for this delivery method.")
            ]));
        }

        var pickupPoint = await dbContext.PickupPoints
            .SingleOrDefaultAsync(point => point.Id == pickupPointId.Value, cancellationToken);
        if (pickupPoint is null || !pickupPoint.IsActive)
        {
            return Result<PickupPoint?>.Failure(
                Error.NotFound("Orders.PickupPointNotFound", "Pickup point was not found."));
        }

        if (!pickupPoint.MatchesAddress(deliveryAddress.CountryCode, deliveryAddress.Province))
        {
            return Result<PickupPoint?>.Failure(Error.Validation([
                new ValidationFailure("pickupPointId", "Pickup point does not serve the selected delivery address.")
            ]));
        }

        return Result<PickupPoint?>.Success(pickupPoint);
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
                : Result<OrderDeliveryAddress>.Success(await VerifyAddressAsync(ToVerificationRequest(saved), cancellationToken));
        }

        return request.DeliveryAddress is null
            ? Result<OrderDeliveryAddress>.Failure(Error.Validation([
                new ValidationFailure("deliveryAddress", "A delivery address is required.")
            ]))
            : Result<OrderDeliveryAddress>.Success(await VerifyAddressAsync(ToVerificationRequest(request.DeliveryAddress), cancellationToken));
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
            address.DeliveryInstructions,
            address.VerificationStatus,
            address.VerificationProvider,
            address.VerificationWarningsJson,
            address.VerifiedAtUtc);

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

    private async Task<OrderDeliveryAddress> VerifyAddressAsync(
        AddressVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var verification = await addressVerificationService.VerifyAsync(request, cancellationToken);
        return new OrderDeliveryAddress(
            verification.RecipientName,
            verification.PhoneNumber,
            verification.AddressLine1,
            verification.AddressLine2,
            verification.Suburb,
            verification.City,
            verification.Province,
            verification.PostalCode,
            verification.CountryCode,
            verification.DeliveryInstructions,
            verification.Status,
            verification.Provider,
            AddressVerificationWarningsJson.Serialize(verification.Warnings),
            verification.VerifiedAtUtc);
    }

    private static AddressVerificationRequest ToVerificationRequest(BuyerDeliveryAddress address) =>
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

    private static AddressVerificationRequest ToVerificationRequest(OrderDeliveryAddressRequest address) =>
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
                address.DeliveryInstructions,
                address.VerificationStatus.ToString(),
                address.VerificationProvider,
                AddressVerificationWarningsJson.Deserialize(address.VerificationWarningsJson),
                address.VerifiedAtUtc);

    private static OrderPickupPointResult? MapPickupPoint(PickupPointSnapshot? pickupPoint) =>
        pickupPoint is null
            ? null
            : new OrderPickupPointResult(
                pickupPoint.PickupPointId,
                pickupPoint.ProviderName,
                pickupPoint.Code,
                pickupPoint.Name,
                pickupPoint.AddressLine1,
                pickupPoint.AddressLine2,
                pickupPoint.Suburb,
                pickupPoint.City,
                pickupPoint.Province,
                pickupPoint.PostalCode,
                pickupPoint.CountryCode,
                pickupPoint.Latitude,
                pickupPoint.Longitude,
                pickupPoint.OpeningHours);

    private static SellerPolicySnapshotResponse? MapSellerPolicySnapshot(OrderSellerPolicySnapshot? snapshot) =>
        snapshot is null
            ? null
            : new SellerPolicySnapshotResponse(
                snapshot.ReturnWindowDays,
                snapshot.ReturnPolicy,
                snapshot.ExchangePolicy,
                snapshot.FulfilmentPolicy,
                snapshot.SupportPolicy,
                snapshot.CareInstructions,
                snapshot.ProductDisclaimer,
                snapshot.SnapshotAtUtc);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private sealed record ResolvedDeliveryMethod(
        SellerDeliveryMethod DeliveryMethod,
        decimal ShippingAmount);
}
