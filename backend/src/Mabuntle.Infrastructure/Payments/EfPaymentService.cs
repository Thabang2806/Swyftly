using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Mabuntle.Application.Advertising;
using Mabuntle.Application.Analytics;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;
using Mabuntle.Application.Ledger;
using Mabuntle.Application.Payments;
using Mabuntle.Domain.Carts;
using Mabuntle.Domain.Inventory;
using Mabuntle.Domain.Orders;
using Mabuntle.Domain.Payments;
using Mabuntle.Infrastructure.Inventory;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Payments;

public sealed class EfPaymentService(
    MabuntleDbContext dbContext,
    IPaymentProvider paymentProvider,
    ILedgerService ledgerService,
    IAdTrackingService adTrackingService,
    IStorefrontAnalyticsService storefrontAnalyticsService,
    IBuyerGrowthOutcomeAttributionService buyerGrowthOutcomeAttributionService,
    IOptions<PaymentProviderOptions> paymentOptions,
    TimeProvider timeProvider) : IPaymentService
{
    private readonly PaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public async Task<Result<PaymentInitiationResponse>> InitiatePaymentAsync(
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BuyerId == Guid.Empty || request.OrderId == Guid.Empty)
        {
            return Result<PaymentInitiationResponse>.Failure(Error.Validation([
                new ValidationFailure("payment", "Buyer id and order id are required.")
            ]));
        }

        var order = await dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order => order.Id == request.OrderId && order.BuyerId == request.BuyerId,
                cancellationToken);
        if (order is null)
        {
            return Result<PaymentInitiationResponse>.Failure(
                Error.NotFound("Payments.OrderNotFound", "Order was not found."));
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return Result<PaymentInitiationResponse>.Failure(
                Error.Conflict("Payments.OrderNotPendingPayment", "Only pending-payment orders can start payment."));
        }

        var existingActivePayment = await FindActivePaymentAsync(order.Id, cancellationToken);
        if (existingActivePayment is not null)
        {
            return Result<PaymentInitiationResponse>.Success(Map(existingActivePayment));
        }

        var now = timeProvider.GetUtcNow();
        var payment = new Payment(
            order.Id,
            order.BuyerId,
            paymentProvider.ProviderName,
            order.TotalAmount,
            _paymentOptions.DefaultCurrency,
            now);
        dbContext.Payments.Add(payment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(payment).State = EntityState.Detached;
                existingActivePayment = await FindActivePaymentAsync(order.Id, cancellationToken);
                if (existingActivePayment is not null)
                {
                    return Result<PaymentInitiationResponse>.Success(Map(existingActivePayment));
                }

            throw;
        }

        var providerResult = await paymentProvider.InitializePaymentAsync(
            new PaymentInitiationRequest(
                order.Id,
                order.BuyerId,
                order.TotalAmount,
                _paymentOptions.DefaultCurrency,
                $"Mabuntle order {order.Id}",
                AppendQueryParameter(new Uri(_paymentOptions.SuccessRedirectUrl), "orderId", order.Id.ToString()),
                AppendQueryParameter(new Uri(_paymentOptions.FailureRedirectUrl), "orderId", order.Id.ToString()),
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id.ToString(),
                    ["paymentId"] = payment.Id.ToString()
                }),
            cancellationToken);

        if (providerResult.IsFailure)
        {
            payment.MarkFailed(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<PaymentInitiationResponse>.Failure(providerResult.Error);
        }

        payment.SetProviderReference(providerResult.Value.ProviderReference, timeProvider.GetUtcNow());
        payment.SetCheckoutUrl(providerResult.Value.CheckoutUrl, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<PaymentInitiationResponse>.Success(Map(payment));
    }

    public async Task<Result<PaymentWebhookProcessingResult>> ProcessWebhookAsync(
        ProcessPaymentWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Provider, paymentProvider.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PaymentWebhookProcessingResult>.Failure(
                Error.NotFound("Payments.ProviderNotFound", "Payment provider was not found."));
        }

        var signatureResult = await paymentProvider.VerifyWebhookSignatureAsync(
            new PaymentWebhookSignatureVerificationRequest(request.Payload, request.Headers),
            cancellationToken);
        if (signatureResult.IsFailure)
        {
            return Result<PaymentWebhookProcessingResult>.Failure(signatureResult.Error);
        }

        var parsedResult = await paymentProvider.ParseWebhookAsync(
            new PaymentWebhookParseRequest(request.Payload, request.Headers),
            cancellationToken);
        if (parsedResult.IsFailure)
        {
            return Result<PaymentWebhookProcessingResult>.Failure(parsedResult.Error);
        }

        var parsedEvent = parsedResult.Value;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var duplicateEvent = await dbContext.PaymentEvents.SingleOrDefaultAsync(
            paymentEvent => paymentEvent.Provider == parsedEvent.Provider
                && paymentEvent.ProviderEventId == parsedEvent.EventId,
            cancellationToken);
        if (duplicateEvent is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<PaymentWebhookProcessingResult>.Success(new PaymentWebhookProcessingResult(
                duplicateEvent.Id,
                duplicateEvent.PaymentId,
                duplicateEvent.ProviderEventId,
                duplicateEvent.ProcessingStatus.ToString(),
                "Unchanged",
                null));
        }

        var now = timeProvider.GetUtcNow();
        var payment = await dbContext.Payments.SingleOrDefaultAsync(
            payment => payment.Provider == parsedEvent.Provider
                && payment.ProviderReference == parsedEvent.ProviderReference,
            cancellationToken);
        var paymentEvent = new PaymentEvent(
            payment?.Id,
            parsedEvent.Provider,
            parsedEvent.EventId,
            parsedEvent.EventType,
            PaymentWebhookPayloadSanitizer.Sanitize(parsedEvent.Provider, parsedEvent.Payload),
            now);
        dbContext.PaymentEvents.Add(paymentEvent);

        var shouldRecordOrderPaidFunnelEvent = false;

        if (payment is null)
        {
            paymentEvent.MarkFailed("Payment was not found for provider reference.", now);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicatePaymentEventViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                return await ReturnExistingPaymentEventResultAsync(parsedEvent, cancellationToken);
            }

            return Result<PaymentWebhookProcessingResult>.Failure(
                Error.NotFound("Payments.PaymentNotFound", "Payment was not found for provider reference."));
        }

        var order = await dbContext.Orders
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == payment.OrderId, cancellationToken);

        var providerPayloadMismatch = GetProviderPayloadMismatch(payment, parsedEvent);
        if (providerPayloadMismatch is not null)
        {
            paymentEvent.MarkFailed(providerPayloadMismatch, now);
        }
        else if (IsPaidStatus(parsedEvent.Status))
        {
            if (payment.Status is PaymentStatus.Failed or PaymentStatus.Cancelled || order.Status == OrderStatus.Cancelled)
            {
                paymentEvent.MarkFailed("Paid webhook ignored because payment or order is already terminal.", now);
            }
            else
            {
                await ProcessSuccessfulPaymentAsync(payment, order, now, cancellationToken);
                paymentEvent.MarkProcessed(payment.Id, now);
                shouldRecordOrderPaidFunnelEvent = true;
            }
        }
        else if (IsAuthorizedStatus(parsedEvent.Status))
        {
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Authorized)
            {
                payment.MarkAuthorized(now);
                paymentEvent.MarkProcessed(payment.Id, now);
            }
            else
            {
                paymentEvent.MarkFailed("Authorized webhook ignored because payment is already terminal.", now);
            }
        }
        else if (IsFailedStatus(parsedEvent.Status))
        {
            if (payment.Status is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded || order.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.ReadyToShip or OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Completed or OrderStatus.Refunded)
            {
                paymentEvent.MarkFailed("Failed webhook ignored because payment or order is already settled.", now);
            }
            else
            {
                await ProcessFailedPaymentAsync(payment, order, now, cancellationToken);
                paymentEvent.MarkProcessed(payment.Id, now);
            }
        }
        else
        {
            paymentEvent.MarkProcessed(payment.Id, now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (shouldRecordOrderPaidFunnelEvent)
            {
                await storefrontAnalyticsService.RecordOrderPaidAsync(order.Id, cancellationToken);
                await buyerGrowthOutcomeAttributionService.RecordOrderPaidAsync(order.Id, now, cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (IsDuplicatePaymentEventViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ReturnExistingPaymentEventResultAsync(parsedEvent, cancellationToken);
        }

        return Result<PaymentWebhookProcessingResult>.Success(new PaymentWebhookProcessingResult(
            paymentEvent.Id,
            payment.Id,
            paymentEvent.ProviderEventId,
            paymentEvent.ProcessingStatus.ToString(),
            payment.Status.ToString(),
            order.Status.ToString()));
    }

    private async Task ProcessSuccessfulPaymentAsync(
        Payment payment,
        Order order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        payment.MarkPaid(now);
        order.ChangeStatus(OrderStatus.Paid, now, "PaymentConfirmed");
        TrackLatestOrderStatusHistory(order);

        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == order.CartId && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            var alreadyRecorded = await dbContext.InventoryMovements.AnyAsync(
                movement => movement.MovementType == InventoryMovementType.ReservationConfirmed
                    && movement.ReservationId == reservation.Id
                    && movement.PaymentId == payment.Id,
                cancellationToken);
            var snapshot = alreadyRecorded
                ? null
                : await InventoryMovementRecorder.LoadSnapshotAsync(
                    dbContext,
                    reservation.ProductVariantId,
                    cancellationToken);
            reservation.Confirm(now);
            if (!alreadyRecorded && snapshot is not null)
            {
                dbContext.InventoryMovements.Add(InventoryMovementRecorder.CreateContext(
                    snapshot,
                    InventoryMovementType.ReservationConfirmed,
                    "PaymentWebhookPaid",
                    "Signed payment webhook confirmed the checkout reservation; stock quantity was not deducted automatically.",
                    actorUserId: null,
                    batchReference: null,
                    occurredAtUtc: now,
                    cartId: order.CartId,
                    orderId: order.Id,
                    reservationId: reservation.Id,
                    paymentId: payment.Id));
            }
        }

        var ledgerResult = await ledgerService.CreateSuccessfulPaymentEntriesAsync(
            new SuccessfulPaymentLedgerRequest(
                payment.Id,
                order.Id,
                order.BuyerId,
                order.SellerId,
                payment.Amount,
                payment.Currency,
                now),
            cancellationToken);
        if (ledgerResult.IsFailure)
        {
            throw new InvalidOperationException(ledgerResult.Error.Description);
        }

        await adTrackingService.AttributeOrderConversionsAsync(order.Id, cancellationToken);

        var cart = await dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.Id == order.CartId && cart.Status == CartStatus.Active,
                cancellationToken);
        cart?.Clear();
    }

    private async Task ProcessFailedPaymentAsync(
        Payment payment,
        Order order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        payment.MarkFailed(now);
        order.ChangeStatus(OrderStatus.Cancelled, now, "PaymentFailed");
        TrackLatestOrderStatusHistory(order);

        var reservations = await dbContext.InventoryReservations
            .Where(reservation => reservation.CartId == order.CartId && reservation.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            var beforeRelease = await InventoryMovementRecorder.LoadSnapshotAsync(
                dbContext,
                reservation.ProductVariantId,
                cancellationToken);
            var variant = await dbContext.ProductVariants.SingleOrDefaultAsync(
                variant => variant.Id == reservation.ProductVariantId,
                cancellationToken);
            if (variant is not null)
            {
                variant.ReleaseReservation(reservation.Quantity);
            }

            reservation.Cancel(now);
            if (variant is not null && beforeRelease is not null)
            {
                var afterRelease = beforeRelease with
                {
                    ReservedQuantity = beforeRelease.ReservedQuantity - reservation.Quantity
                };
                var alreadyRecorded = await dbContext.InventoryMovements.AnyAsync(
                    movement => movement.MovementType == InventoryMovementType.PaymentFailedReservationReleased
                        && movement.ReservationId == reservation.Id
                        && movement.PaymentId == payment.Id,
                    cancellationToken);
                if (!alreadyRecorded)
                {
                    dbContext.InventoryMovements.Add(InventoryMovementRecorder.Create(
                        beforeRelease,
                        afterRelease,
                        InventoryMovementType.PaymentFailedReservationReleased,
                        "PaymentWebhookFailed",
                        "Payment failed or was cancelled before settlement, so the checkout reservation was released.",
                        actorUserId: null,
                        batchReference: null,
                        occurredAtUtc: now,
                        cartId: order.CartId,
                        orderId: order.Id,
                        reservationId: reservation.Id,
                        paymentId: payment.Id));
                }
            }
        }
    }

    private static PaymentInitiationResponse Map(Payment payment) =>
        new(
            payment.Id,
            payment.OrderId,
            payment.Provider,
            payment.ProviderReference,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            TryCreateUri(payment.CheckoutUrl));

    private async Task<Payment?> FindActivePaymentAsync(
        Guid orderId,
        CancellationToken cancellationToken) =>
        await dbContext.Payments
            .Where(payment => payment.OrderId == orderId
                && payment.Status != PaymentStatus.Failed
                && payment.Status != PaymentStatus.Cancelled)
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Result<PaymentWebhookProcessingResult>> ReturnExistingPaymentEventResultAsync(
        PaymentWebhookEvent parsedEvent,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var existingEvent = await dbContext.PaymentEvents
            .AsNoTracking()
            .SingleAsync(
                paymentEvent => paymentEvent.Provider == parsedEvent.Provider
                    && paymentEvent.ProviderEventId == parsedEvent.EventId,
                cancellationToken);

        return Result<PaymentWebhookProcessingResult>.Success(new PaymentWebhookProcessingResult(
            existingEvent.Id,
            existingEvent.PaymentId,
            existingEvent.ProviderEventId,
            existingEvent.ProcessingStatus.ToString(),
            "Unchanged",
            null));
    }

    private static bool IsDuplicatePaymentEventViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && exception.Entries.Any(entry => entry.Entity is PaymentEvent);

    private void TrackLatestOrderStatusHistory(Order order)
    {
        var statusHistory = order.StatusHistory.LastOrDefault();
        if (statusHistory is not null)
        {
            dbContext.OrderStatusHistory.Add(statusHistory);
        }
    }

    private static bool IsPaidStatus(string status) =>
        string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Captured", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Complete", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthorizedStatus(string status) =>
        string.Equals(status, "Authorized", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedStatus(string status) =>
        string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);

    private static string? GetProviderPayloadMismatch(Payment payment, PaymentWebhookEvent parsedEvent)
    {
        if (!string.Equals(payment.ProviderReference, parsedEvent.ProviderReference, StringComparison.Ordinal))
        {
            return "Webhook provider reference does not match the local payment.";
        }

        if (parsedEvent.Amount.HasValue
            && decimal.Round(parsedEvent.Amount.Value, 2) != decimal.Round(payment.Amount, 2))
        {
            return "Webhook amount does not match the local payment amount.";
        }

        if (!string.IsNullOrWhiteSpace(parsedEvent.Currency)
            && !string.Equals(parsedEvent.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return "Webhook currency does not match the local payment currency.";
        }

        return null;
    }

    private static Uri? TryCreateUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;

    private static Uri AppendQueryParameter(Uri uri, string name, string value)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
    }
}
