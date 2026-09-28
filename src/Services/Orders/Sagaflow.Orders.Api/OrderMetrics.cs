using System.Diagnostics.Metrics;
using Sagaflow.ServiceDefaults;

namespace Sagaflow.Orders.Api;

/// <summary>Business metrics for the order funnel, exported through OpenTelemetry to Prometheus.</summary>
public sealed class OrderMetrics
{
    private readonly Counter<long> _submitted = Telemetry.Meter.CreateCounter<long>("sagaflow.orders.submitted", "{order}");
    private readonly Counter<long> _completed = Telemetry.Meter.CreateCounter<long>("sagaflow.orders.completed", "{order}");
    private readonly Counter<long> _cancelled = Telemetry.Meter.CreateCounter<long>("sagaflow.orders.cancelled", "{order}");
    private readonly Counter<double> _revenue = Telemetry.Meter.CreateCounter<double>("sagaflow.orders.revenue", "BRL");
    private readonly Histogram<double> _duration = Telemetry.Meter.CreateHistogram<double>(
        "sagaflow.orders.duration", "s", "Time from submission to the order's final state");

    public void OrderSubmitted() => _submitted.Add(1);

    public void OrderCompleted(decimal total, TimeSpan duration)
    {
        _completed.Add(1);
        _revenue.Add((double)total);
        _duration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", "completed"));
    }

    public void OrderCancelled(TimeSpan duration)
    {
        _cancelled.Add(1);
        _duration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", "cancelled"));
    }
}
