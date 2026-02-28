using Prometheus;

namespace PaymentService.Infrastructure.Messaging;

public sealed class OutboxMetrics
{
    public readonly Histogram ConsumerLatency = Metrics.CreateHistogram(
        "outbox_consumer_latency_seconds",
        "Time from outbox record creation to payment processing completion",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 12)
        });
}
