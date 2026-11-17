using KafkaFlow;
using PaymentService.Application.Events;

namespace PaymentService.Infrastructure.Messaging;

public class OrderProcessingMetricsMiddleware(OutboxMetrics metrics) : IMessageMiddleware
{
    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        await next(context);

        if (context.Message.Value is not OrderCreatedEvent ||
            !DateTimeOffset.TryParse(context.Headers.GetString("x-request-started-at"), out var startedAt))
            return;

        var elapsed = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
        if (elapsed >= 0)
            metrics.OrderProcessingLatency.Observe(elapsed);
    }
}
