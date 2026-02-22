using KafkaFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Events;
using PaymentService.Application.Interfaces;

namespace PaymentService.Infrastructure.Messaging;

public class OrderCreatedHandler(IServiceProvider serviceProvider, ILogger<OrderCreatedHandler> logger)
    : IMessageHandler<OrderCreatedEvent>
{
    public async Task Handle(IMessageContext context, OrderCreatedEvent message)
    {
        var idempotencyKey = context.Headers.GetString("idempotency-key") is { } raw
            ? Guid.Parse(raw)
            : Guid.NewGuid();

        logger.LogInformation("Received OrderCreatedEvent: OrderId={OrderId}, Price={Price}, IdempotencyKey={IdempotencyKey}",
            message.Id, message.Price, idempotencyKey);

        using var scope = serviceProvider.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        await paymentService.ProcessOrderAsync(message.Id, message.Price, idempotencyKey);

        context.ConsumerContext.Complete();
        logger.LogInformation("Payment processed for OrderId={OrderId}", message.Id);
    }
}
