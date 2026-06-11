using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Orders;

namespace Mabuntle.Infrastructure.Carriers;

public sealed class ManualCarrierProvider : ICarrierProvider
{
    public const string Name = "Manual";

    public string ProviderName => Name;

    public Task<Result<CarrierBookingResult>> BookShipmentAsync(
        CarrierBookingProviderRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<CarrierBookingResult>.Failure(
            Error.Conflict(
                "CarrierProvider.ManualMode",
                "Carrier booking is disabled while CarrierProvider:ProviderName is Manual. Use manual tracking instead.")));

    public Task<Result<CarrierTrackingResult>> GetTrackingAsync(
        CarrierTrackingProviderRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<CarrierTrackingResult>.Failure(
            Error.Conflict(
                "CarrierProvider.ManualMode",
                "Carrier tracking sync is disabled while CarrierProvider:ProviderName is Manual.")));
}
