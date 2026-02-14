using System.Diagnostics;
using KafkaFlow;

namespace OrderService.Infrastructure.Messaging;

public class TracingProducerMiddleware : IMessageMiddleware
{
    private static readonly ActivitySource ActivitySource = new("OrderService.Kafka");

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        using var activity = ActivitySource.StartActivity("kafka produce", ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag("messaging.system", "kafka");
            activity.SetTag("messaging.destination.name", context.ConsumerContext?.Topic ?? "unknown");
            activity.SetTag("messaging.operation", "publish");

            var traceparent = activity.Id;
            if (traceparent is not null)
            {
                context.Headers.SetString("traceparent", traceparent);
            }

            var tracestate = activity.TraceStateString;
            if (tracestate is not null)
            {
                context.Headers.SetString("tracestate", tracestate);
            }
        }

        await next(context);
    }
}
