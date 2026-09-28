using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sagaflow.ServiceDefaults;

public static class Telemetry
{
    /// <summary>ActivitySource / Meter name used by MassTransit's built-in instrumentation.</summary>
    public const string MassTransitSource = "MassTransit";

    /// <summary>ActivitySource / Meter name for Sagaflow's own business telemetry.</summary>
    public const string SagaflowSource = "Sagaflow";

    public static readonly ActivitySource ActivitySource = new(SagaflowSource);

    public static readonly Meter Meter = new(SagaflowSource);
}
