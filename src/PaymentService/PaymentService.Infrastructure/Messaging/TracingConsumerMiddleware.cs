using System.Diagnostics;
using KafkaFlow;

namespace PaymentService.Infrastructure.Messaging;

public class TracingConsumerMiddleware : IMessageMiddleware
{
    private static readonly ActivitySource ActivitySource = new("PaymentService.Kafka");

    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        var parentContext = default(ActivityContext);

        var traceparent = context.Headers.GetString("traceparent");
        if (traceparent is not null && ActivityContext.TryParse(traceparent, null, out var parsed))
        {
            parentContext = parsed;
        }

        using var activity = ActivitySource.StartActivity(
            "kafka consume",
            ActivityKind.Consumer,
            parentContext);

        if (activity is not null)
        {
            activity.SetTag("messaging.system", "kafka");
            activity.SetTag("messaging.destination.name", context.ConsumerContext.Topic);
            activity.SetTag("messaging.operation", "process");
            activity.SetTag("messaging.kafka.consumer.group", context.ConsumerContext.GroupId);
        }

        await next(context);
    }
}
