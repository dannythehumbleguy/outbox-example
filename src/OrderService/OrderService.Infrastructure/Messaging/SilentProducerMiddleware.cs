using KafkaFlow;
using Microsoft.Extensions.Logging;

namespace OrderService.Infrastructure.Messaging;

public class SilentProducerMiddleware(ILogger<SilentProducerMiddleware> logger) : IMessageMiddleware
{
    public async Task Invoke(IMessageContext context, MiddlewareDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to produce message to Kafka — event lost");
        }
    }
}
