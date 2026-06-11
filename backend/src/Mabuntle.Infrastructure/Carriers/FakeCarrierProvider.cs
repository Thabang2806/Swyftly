using Microsoft.Extensions.Options;
using Mabuntle.Application.Orders;
using Mabuntle.Application.Common.Results;
using Mabuntle.Domain.Orders;

namespace Mabuntle.Infrastructure.Carriers;

public sealed class FakeCarrierProvider(IOptions<CarrierProviderOptions> options, TimeProvider timeProvider) : ICarrierProvider
{
    public const string Name = "Fake";

    private readonly CarrierProviderOptions options = options.Value;

    public string ProviderName => Name;

    public Task<Result<CarrierBookingResult>> BookShipmentAsync(
        CarrierBookingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var reference = CreateProviderReference(request.ShipmentId);
        var trackingNumber = CreateTrackingNumber(request.OrderId);
        var result = new CarrierBookingResult(
            Name,
            request.ServiceCode.Trim().ToUpperInvariant(),
            reference,
            "Fake Courier",
            trackingNumber,
            BuildUrl(options.Fake.TrackingBaseUrl, trackingNumber),
            BuildUrl(options.Fake.LabelBaseUrl, reference),
            CarrierProviderShipmentStatus.Booked,
            timeProvider.GetUtcNow());

        return Task.FromResult(Result<CarrierBookingResult>.Success(result));
    }

    public Task<Result<CarrierTrackingResult>> GetTrackingAsync(
        CarrierTrackingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var nextStatus = request.CurrentProviderStatus?.Trim() switch
        {
            nameof(CarrierProviderShipmentStatus.Booked) => CarrierProviderShipmentStatus.Collected,
            nameof(CarrierProviderShipmentStatus.LabelCreated) => CarrierProviderShipmentStatus.Collected,
            nameof(CarrierProviderShipmentStatus.Collected) => CarrierProviderShipmentStatus.InTransit,
            nameof(CarrierProviderShipmentStatus.InTransit) => CarrierProviderShipmentStatus.Delivered,
            nameof(CarrierProviderShipmentStatus.Delivered) => CarrierProviderShipmentStatus.Delivered,
            nameof(CarrierProviderShipmentStatus.DeliveryFailed) => CarrierProviderShipmentStatus.DeliveryFailed,
            nameof(CarrierProviderShipmentStatus.ReturnedToSender) => CarrierProviderShipmentStatus.ReturnedToSender,
            _ => CarrierProviderShipmentStatus.Collected
        };

        var result = new CarrierTrackingResult(
            Name,
            request.ProviderShipmentReference,
            nextStatus,
            $"Fake carrier status is {nextStatus}.",
            timeProvider.GetUtcNow());

        return Task.FromResult(Result<CarrierTrackingResult>.Success(result));
    }

    private static string CreateProviderReference(Guid shipmentId) =>
        $"fake-shp-{shipmentId:N}";

    private static string CreateTrackingNumber(Guid orderId) =>
        $"FAKE-{orderId:N}"[..17].ToUpperInvariant();

    private static string BuildUrl(string baseUrl, string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://localhost:4200/fake-carrier"
            : baseUrl.TrimEnd('/');

        return $"{trimmed}/{Uri.EscapeDataString(value)}";
    }
}
