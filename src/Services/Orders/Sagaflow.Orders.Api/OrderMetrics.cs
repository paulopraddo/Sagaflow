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
    // OTel's default buckets (0, 5, 10, 25...) suit milliseconds; orders settle in well under a second
    // on the happy path and up to the payment timeout on the slowest one.
    private readonly Histogram<double> _duration = Telemetry.Meter.CreateHistogram(
        "sagaflow.orders.duration",
        "s",
        "Time from submission to the order's final state",
        tags: null,
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 60] });

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
