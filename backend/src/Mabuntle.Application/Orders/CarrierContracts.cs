using Mabuntle.Application.Common.Results;
using Mabuntle.Domain.Orders;

namespace Mabuntle.Application.Orders;

public interface ICarrierProvider
{
    string ProviderName { get; }

    Task<Result<CarrierBookingResult>> BookShipmentAsync(
        CarrierBookingProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CarrierTrackingResult>> GetTrackingAsync(
        CarrierTrackingProviderRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICarrierTrackingSyncService
{
    Task<Result<OrderResult>> BookCarrierAsync(
        CarrierBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderResult>> SyncCarrierTrackingAsync(
        CarrierTrackingSyncRequest request,
        CancellationToken cancellationToken = default);

    Task<CarrierTrackingSyncBatchResult> SyncDueShipmentsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record CarrierBookingRequest(
    Guid SellerId,
    Guid OrderId,
    decimal PackageWeightKg,
    decimal PackageLengthCm,
    decimal PackageWidthCm,
    decimal PackageHeightCm,
    string ServiceCode,
    string? CollectionNote,
    DateTimeOffset OccurredAtUtc);

public sealed record CarrierTrackingSyncRequest(
    Guid SellerId,
    Guid OrderId,
    DateTimeOffset OccurredAtUtc);

public sealed record CarrierBookingProviderRequest(
    Guid SellerId,
    Guid BuyerId,
    Guid OrderId,
    Guid ShipmentId,
    string ServiceCode,
    decimal PackageWeightKg,
    decimal PackageLengthCm,
    decimal PackageWidthCm,
    decimal PackageHeightCm,
    string? CollectionNote,
    OrderDeliveryAddressResult? DeliveryAddress);

public sealed record CarrierBookingResult(
    string ProviderName,
    string ServiceCode,
    string ProviderShipmentReference,
    string CarrierName,
    string TrackingNumber,
    string? TrackingUrl,
    string? LabelUrl,
    CarrierProviderShipmentStatus ProviderStatus,
    DateTimeOffset ProviderStatusUpdatedAtUtc);

public sealed record CarrierTrackingProviderRequest(
    Guid OrderId,
    Guid ShipmentId,
    string ProviderShipmentReference,
    string? CurrentProviderStatus);

public sealed record CarrierTrackingResult(
    string ProviderName,
    string ProviderShipmentReference,
    CarrierProviderShipmentStatus ProviderStatus,
    string? Message,
    DateTimeOffset ProviderStatusUpdatedAtUtc);

public sealed record CarrierTrackingSyncBatchResult(
    int ProcessedCount,
    int UpdatedCount,
    int FailedCount);
