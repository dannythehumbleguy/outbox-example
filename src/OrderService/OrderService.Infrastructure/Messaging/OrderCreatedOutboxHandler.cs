using System.Text.Json;
using OrderService.Application.Constants;
using OrderService.Application.Events;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class OrderCreatedOutboxHandler(IEventPublisher eventPublisher) : IOutboxMessageHandler
{
    public string MessageType => nameof(OrderCreatedEvent);

    public async Task HandleAsync(string payload, Guid messageId)
    {
        var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(payload)!;
        await eventPublisher.PublishAsync(KafkaTopics.OrderEvents, evt, messageId);
    }
}
