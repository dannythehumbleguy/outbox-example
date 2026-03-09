using System.Text.Json;
using OrderService.Application.Constants;
using OrderService.Application.Events;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class OrderCreatedOutboxProducer(IEventPublisher eventPublisher) : IOutboxMessageProducer
{
    public string MessageType => nameof(OrderCreatedEvent);

    public async Task PublishAsync(string payload, Guid messageId)
    {
        var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(payload)!;
        await eventPublisher.PublishAsync(KafkaTopics.OrderEvents, evt, messageId);
    }
}
