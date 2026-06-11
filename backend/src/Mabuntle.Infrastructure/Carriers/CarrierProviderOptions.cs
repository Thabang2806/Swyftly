namespace Mabuntle.Infrastructure.Carriers;

public sealed class CarrierProviderOptions
{
    public const string SectionName = "CarrierProvider";

    public string ProviderName { get; set; } = ManualCarrierProvider.Name;

    public FakeCarrierProviderOptions Fake { get; set; } = new();
}

public sealed class FakeCarrierProviderOptions
{
    public string TrackingBaseUrl { get; set; } = "http://localhost:4200/fake-tracking";

    public string LabelBaseUrl { get; set; } = "http://localhost:4200/fake-label";
}
