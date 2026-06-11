using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Notifications;
using Mabuntle.Application.Orders;
using Mabuntle.Domain.Orders;
using Mabuntle.Infrastructure.Orders;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Carriers;

public sealed class EfCarrierTrackingSyncService(
    MabuntleDbContext dbContext,
    ICarrierProvider carrierProvider,
    INotificationService notificationService,
    IOptions<CarrierTrackingOptions> options,
    ILogger<EfCarrierTrackingSyncService> logger) : ICarrierTrackingSyncService
{
    private readonly CarrierTrackingOptions options = options.Value;

    public async Task<Result<OrderResult>> BookCarrierAsync(
        CarrierBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBooking(request);
        if (validation is not null)
        {
            return validation;
        }

        var order = await GetSellerOrderAsync(request.SellerId, request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.ReadyToShip)
        {
            return InvalidTransition("Carrier booking requires an order that is ready to ship.");
        }

        var shipment = LatestShipment(order);
        if (shipment is null || shipment.Status != ShipmentStatus.ReadyForCourier)
        {
            return InvalidTransition("Carrier booking requires the latest shipment to be ready for courier.");
        }

        if (shipment.CarrierBookingStatus == CarrierBookingStatus.Booked
            && !string.IsNullOrWhiteSpace(shipment.ProviderShipmentReference))
        {
            return Result<OrderResult>.Success(EfOrderFulfillmentService.Map(order));
        }

        var booking = await carrierProvider.BookShipmentAsync(
            new CarrierBookingProviderRequest(
                order.SellerId,
                order.BuyerId,
                order.Id,
                shipment.Id,
                request.ServiceCode.Trim(),
                request.PackageWeightKg,
                request.PackageLengthCm,
                request.PackageWidthCm,
                request.PackageHeightCm,
                request.CollectionNote,
                order.DeliveryAddress is null
                    ? null
                    : new OrderDeliveryAddressResult(
                        order.DeliveryAddress.RecipientName,
                        order.DeliveryAddress.PhoneNumber,
                        order.DeliveryAddress.AddressLine1,
                        order.DeliveryAddress.AddressLine2,
                        order.DeliveryAddress.Suburb,
                        order.DeliveryAddress.City,
                        order.DeliveryAddress.Province,
                        order.DeliveryAddress.PostalCode,
                        order.DeliveryAddress.CountryCode,
                        order.DeliveryAddress.DeliveryInstructions)),
            cancellationToken);

        if (booking.IsFailure)
        {
            return Result<OrderResult>.Failure(booking.Error);
        }

        var eventCount = shipment.Events.Count;
        shipment.BookCarrier(
            booking.Value.ProviderName,
            booking.Value.ServiceCode,
            booking.Value.ProviderShipmentReference,
            booking.Value.CarrierName,
            booking.Value.TrackingNumber,
            booking.Value.TrackingUrl,
            booking.Value.LabelUrl,
            booking.Value.ProviderStatus,
            request.PackageWeightKg,
            request.PackageLengthCm,
            request.PackageWidthCm,
            request.PackageHeightCm,
            request.OccurredAtUtc);
        TrackNewShipmentEvents(shipment, eventCount);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<OrderResult>.Success(EfOrderFulfillmentService.Map(order));
    }

    public async Task<Result<OrderResult>> SyncCarrierTrackingAsync(
        CarrierTrackingSyncRequest request,
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

        var shipment = LatestShipment(order);
        if (shipment is null
            || shipment.CarrierBookingStatus != CarrierBookingStatus.Booked
            || string.IsNullOrWhiteSpace(shipment.ProviderShipmentReference))
        {
            return InvalidTransition("Carrier tracking sync requires a booked shipment.");
        }

        var tracking = await carrierProvider.GetTrackingAsync(
            new CarrierTrackingProviderRequest(
                order.Id,
                shipment.Id,
                shipment.ProviderShipmentReference,
                shipment.ProviderStatus),
            cancellationToken);

        if (tracking.IsFailure)
        {
            shipment.UpdateProviderStatus(
                ParseProviderStatus(shipment.ProviderStatus) ?? CarrierProviderShipmentStatus.Booked,
                shipment.ProviderStatusUpdatedAtUtc ?? request.OccurredAtUtc,
                request.OccurredAtUtc,
                tracking.Error.Description);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<OrderResult>.Failure(tracking.Error);
        }

        var notificationType = ApplyProviderStatus(order, shipment, tracking.Value, request.OccurredAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (notificationType is not null)
        {
            await NotifyBuyerAsync(order, notificationType, request.OccurredAtUtc, cancellationToken);
        }

        return Result<OrderResult>.Success(EfOrderFulfillmentService.Map(order));
    }

    public async Task<CarrierTrackingSyncBatchResult> SyncDueShipmentsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(carrierProvider.ProviderName, ManualCarrierProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new CarrierTrackingSyncBatchResult(0, 0, 0);
        }

        var cutoff = now.AddMinutes(-Math.Max(1, options.SyncIntervalMinutes));
        var batchSize = Math.Clamp(options.BatchSize, 1, 200);
        var candidates = await dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.CarrierBookingStatus == CarrierBookingStatus.Booked
                && shipment.ProviderShipmentReference != null
                && shipment.ProviderStatus != nameof(CarrierProviderShipmentStatus.Delivered)
                && shipment.ProviderStatus != nameof(CarrierProviderShipmentStatus.ReturnedToSender)
                && (shipment.ProviderLastSyncedAtUtc == null || shipment.ProviderLastSyncedAtUtc <= cutoff))
            .OrderBy(shipment => shipment.ProviderLastSyncedAtUtc ?? shipment.CarrierBookedAtUtc ?? shipment.CreatedAtUtc)
            .Select(shipment => new { shipment.SellerId, shipment.OrderId })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var processed = 0;
        var updated = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            processed++;
            try
            {
                var result = await SyncCarrierTrackingAsync(
                    new CarrierTrackingSyncRequest(candidate.SellerId, candidate.OrderId, now),
                    cancellationToken);
                if (result.IsSuccess)
                {
                    updated++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogError(
                    exception,
                    "Carrier tracking sync failed for order {OrderId}.",
                    candidate.OrderId);
            }
        }

        return new CarrierTrackingSyncBatchResult(processed, updated, failed);
    }

    private string? ApplyProviderStatus(
        Order order,
        Shipment shipment,
        CarrierTrackingResult tracking,
        DateTimeOffset syncedAtUtc)
    {
        var previousProviderStatus = shipment.ProviderStatus;
        var eventCount = shipment.Events.Count;
        var orderHistoryCount = order.StatusHistory.Count;
        var currentProviderStatus = ParseProviderStatus(shipment.ProviderStatus);
        if (currentProviderStatus.HasValue && IsStaleProviderStatus(currentProviderStatus.Value, tracking.ProviderStatus))
        {
            shipment.UpdateProviderStatus(
                currentProviderStatus.Value,
                shipment.ProviderStatusUpdatedAtUtc ?? syncedAtUtc,
                syncedAtUtc);
            return null;
        }

        var statusChanged = shipment.UpdateProviderStatus(
            tracking.ProviderStatus,
            tracking.ProviderStatusUpdatedAtUtc,
            syncedAtUtc);

        if (!statusChanged && string.Equals(previousProviderStatus, tracking.ProviderStatus.ToString(), StringComparison.Ordinal))
        {
            return null;
        }

        string? notificationType = null;

        try
        {
            switch (tracking.ProviderStatus)
            {
                case CarrierProviderShipmentStatus.Booked:
                case CarrierProviderShipmentStatus.LabelCreated:
                    break;
                case CarrierProviderShipmentStatus.Collected:
                    var collectedMarkedShipped = MarkOrderShippedIfNeeded(order, syncedAtUtc);
                    shipment.MarkCollected(syncedAtUtc, tracking.Message);
                    notificationType = collectedMarkedShipped ? "OrderShipped" : null;
                    break;
                case CarrierProviderShipmentStatus.InTransit:
                    var inTransitMarkedShipped = MarkOrderShippedIfNeeded(order, syncedAtUtc);
                    shipment.MarkInTransit(syncedAtUtc, tracking.Message);
                    notificationType = inTransitMarkedShipped ? "OrderShipped" : null;
                    break;
                case CarrierProviderShipmentStatus.Delivered:
                    MarkOrderShippedIfNeeded(order, syncedAtUtc);
                    if (shipment.Status != ShipmentStatus.InTransit)
                    {
                        shipment.MarkInTransit(syncedAtUtc, "Carrier reported delivery after transit.");
                    }

                    if (order.Status != OrderStatus.Delivered || shipment.Status != ShipmentStatus.Delivered)
                    {
                        order.ChangeStatus(OrderStatus.Delivered, syncedAtUtc, "CarrierMarkedDelivered");
                        shipment.MarkDelivered(syncedAtUtc, tracking.Message);
                        notificationType = "OrderDelivered";
                    }
                    break;
                case CarrierProviderShipmentStatus.DeliveryFailed:
                    MarkOrderShippedIfNeeded(order, syncedAtUtc);
                    if (shipment.Status != ShipmentStatus.InTransit)
                    {
                        shipment.MarkInTransit(syncedAtUtc, "Carrier reported a delivery exception after transit.");
                    }

                    shipment.MarkDeliveryFailed(tracking.Message ?? "Carrier reported delivery failed.", syncedAtUtc);
                    notificationType = "OrderDeliveryFailed";
                    break;
                case CarrierProviderShipmentStatus.ReturnedToSender:
                    MarkOrderShippedIfNeeded(order, syncedAtUtc);
                    if (shipment.Status is not (ShipmentStatus.InTransit or ShipmentStatus.DeliveryFailed))
                    {
                        shipment.MarkInTransit(syncedAtUtc, "Carrier reported return to sender after transit.");
                    }

                    shipment.MarkReturnedToSender(tracking.Message ?? "Carrier reported returned to sender.", syncedAtUtc);
                    notificationType = "OrderReturnedToSender";
                    break;
            }
        }
        catch (InvalidOperationException exception)
        {
            shipment.UpdateProviderStatus(
                tracking.ProviderStatus,
                tracking.ProviderStatusUpdatedAtUtc,
                syncedAtUtc,
                exception.Message);
            notificationType = null;
        }

        TrackNewOrderStatusHistories(order, orderHistoryCount);
        TrackNewShipmentEvents(shipment, eventCount);
        return notificationType;
    }

    private static bool MarkOrderShippedIfNeeded(Order order, DateTimeOffset occurredAtUtc)
    {
        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
        {
            return false;
        }

        order.ChangeStatus(OrderStatus.Shipped, occurredAtUtc, "CarrierMarkedShipped");
        return true;
    }

    private async Task NotifyBuyerAsync(
        Order order,
        string notificationType,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var buyerUserId = await dbContext.BuyerProfiles
                .Where(buyer => buyer.Id == order.BuyerId)
                .Select(buyer => buyer.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (buyerUserId == Guid.Empty)
            {
                return;
            }

            var (title, message) = notificationType switch
            {
                "OrderDelivered" => ("Your order was marked delivered", "The carrier marked your order as delivered."),
                "OrderDeliveryFailed" => ("Delivery needs attention", "The carrier reported a delivery issue. Contact support if you need help."),
                "OrderReturnedToSender" => ("Shipment returned to sender", "The carrier reported that the shipment was returned to sender."),
                _ => ("Your order has shipped", "The carrier updated your shipment status.")
            };

            await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    buyerUserId,
                    notificationType,
                    title,
                    message,
                    "Order",
                    order.Id,
                    occurredAtUtc),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to create buyer carrier notification {NotificationType} for order {OrderId}.",
                notificationType,
                order.Id);
        }
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

    private static CarrierProviderShipmentStatus? ParseProviderStatus(string? status) =>
        Enum.TryParse<CarrierProviderShipmentStatus>(status, ignoreCase: false, out var parsed)
            ? parsed
            : null;

    private static bool IsStaleProviderStatus(
        CarrierProviderShipmentStatus current,
        CarrierProviderShipmentStatus next) =>
        (current == CarrierProviderShipmentStatus.DeliveryFailed
            && next is not (CarrierProviderShipmentStatus.DeliveryFailed or CarrierProviderShipmentStatus.ReturnedToSender))
        ||
        (IsTerminal(current) && next != current)
        || StatusRank(next) < StatusRank(current);

    private static int StatusRank(CarrierProviderShipmentStatus status) =>
        status switch
        {
            CarrierProviderShipmentStatus.Booked => 0,
            CarrierProviderShipmentStatus.LabelCreated => 1,
            CarrierProviderShipmentStatus.Collected => 2,
            CarrierProviderShipmentStatus.InTransit => 3,
            CarrierProviderShipmentStatus.Delivered => 4,
            CarrierProviderShipmentStatus.DeliveryFailed => 4,
            CarrierProviderShipmentStatus.ReturnedToSender => 4,
            _ => 0
        };

    private static bool IsTerminal(CarrierProviderShipmentStatus status) =>
        status is CarrierProviderShipmentStatus.Delivered
            or CarrierProviderShipmentStatus.ReturnedToSender;

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

    private static Result<OrderResult>? ValidateBooking(CarrierBookingRequest request)
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

        ValidatePositive(request.PackageWeightKg, "packageWeightKg", failures);
        ValidatePositive(request.PackageLengthCm, "packageLengthCm", failures);
        ValidatePositive(request.PackageWidthCm, "packageWidthCm", failures);
        ValidatePositive(request.PackageHeightCm, "packageHeightCm", failures);

        if (string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            failures.Add(new ValidationFailure("serviceCode", "Service code is required."));
        }
        else if (request.ServiceCode.Trim().Length > Shipment.CarrierServiceCodeMaxLength)
        {
            failures.Add(new ValidationFailure("serviceCode", $"Service code must be {Shipment.CarrierServiceCodeMaxLength} characters or fewer."));
        }

        if (request.CollectionNote?.Trim().Length > Shipment.ExceptionReasonMaxLength)
        {
            failures.Add(new ValidationFailure("collectionNote", $"Collection note must be {Shipment.ExceptionReasonMaxLength} characters or fewer."));
        }

        return failures.Count == 0
            ? null
            : Result<OrderResult>.Failure(Error.Validation(failures));
    }

    private static void ValidatePositive(
        decimal value,
        string propertyName,
        ICollection<ValidationFailure> failures)
    {
        if (value <= 0)
        {
            failures.Add(new ValidationFailure(propertyName, "Value must be greater than zero."));
        }
    }

    private static Result<OrderResult> OrderNotFound() =>
        Result<OrderResult>.Failure(
            Error.NotFound("Orders.NotFound", "Order was not found."));

    private static Result<OrderResult> InvalidTransition(string description) =>
        Result<OrderResult>.Failure(
            Error.Conflict("Carrier.InvalidFulfillmentTransition", description));

    private void TrackNewOrderStatusHistories(Order order, int previousCount)
    {
        if (order.StatusHistory.Count <= previousCount)
        {
            return;
        }

        foreach (var statusHistory in order.StatusHistory.Skip(previousCount))
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private void TrackNewShipmentEvents(Shipment shipment, int previousCount)
    {
        if (shipment.Events.Count <= previousCount)
        {
            return;
        }

        foreach (var shipmentEvent in shipment.Events.Skip(previousCount))
        {
            dbContext.ShipmentEvents.Add(shipmentEvent);
        }
    }
}
