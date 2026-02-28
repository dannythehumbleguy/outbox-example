using Prometheus;

namespace OrderService.Infrastructure.Messaging;

public sealed class OutboxMetrics
{
    public readonly Histogram PublishLatency = Metrics.CreateHistogram(
        "outbox_publish_latency_seconds",
        "Time from outbox record creation to successful Kafka publish",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 12)
        });
}
