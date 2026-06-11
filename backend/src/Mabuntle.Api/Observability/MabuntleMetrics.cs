using System.Diagnostics.Metrics;

namespace Mabuntle.Api.Observability;

public sealed class MabuntleMetrics : IDisposable
{
    public const string MeterName = "Mabuntle.Api";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _paymentEvents;
    private readonly Counter<long> _aiRequests;
    private readonly Counter<long> _orders;
    private readonly Counter<long> _errors;

    public MabuntleMetrics()
    {
        _paymentEvents = _meter.CreateCounter<long>("mabuntle.payments.events");
        _aiRequests = _meter.CreateCounter<long>("mabuntle.ai.requests");
        _orders = _meter.CreateCounter<long>("mabuntle.orders.created");
        _errors = _meter.CreateCounter<long>("mabuntle.errors");
    }

    public void RecordPaymentEvent() => _paymentEvents.Add(1);

    public void RecordAiRequest() => _aiRequests.Add(1);

    public void RecordOrderCreated() => _orders.Add(1);

    public void RecordError() => _errors.Add(1);

    public void Dispose() => _meter.Dispose();
}
