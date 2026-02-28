using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Pefi.Bank.Infrastructure;

/// <summary>
/// Central definition of all OpenTelemetry activity sources and meters for Pefi.Bank.
/// Both the API and Functions hosts register these names with the OTel SDK.
/// </summary>
public static class DiagnosticConfig
{
    public const string ServiceName = "Pefi.Bank";

    // ── Tracing ─────────────────────────────────────────────────────────────
    public static readonly ActivitySource Source = new(ServiceName);

    // ── Metrics ─────────────────────────────────────────────────────────────
    public static readonly Meter Meter = new(ServiceName);

    // Event Store
    public static readonly Counter<long> EventsStored =
        Meter.CreateCounter<long>("pefi.bank.events.stored", "events",
            "Number of domain events written to the event store");

    // Projections
    public static readonly Counter<long> EventsProjected =
        Meter.CreateCounter<long>("pefi.bank.events.projected", "events",
            "Number of domain events processed by projection handlers");

    // Saga
    public static readonly Counter<long> SagasCompleted =
        Meter.CreateCounter<long>("pefi.bank.saga.completed", "sagas",
            "Number of transfer sagas that completed successfully");

    public static readonly Counter<long> SagasFailed =
        Meter.CreateCounter<long>("pefi.bank.saga.failed", "sagas",
            "Number of transfer sagas that failed or required compensation");

    public static readonly Histogram<double> SagaStepDuration =
        Meter.CreateHistogram<double>("pefi.bank.saga.step.duration", "ms",
            "Duration of individual saga steps");
}
