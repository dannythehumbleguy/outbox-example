using KafkaFlow;
using KafkaFlow.Producers;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class KafkaEventPublisher(IProducerAccessor producerAccessor) : IEventPublisher
{
    public Task PublishAsync<T>(string topic, T message, Guid idempotencyKey, DateTimeOffset occurredOn) where T : class
    {
        var producer = producerAccessor.GetProducer(KafkaProducers.OrderEvents);
        var headers = new MessageHeaders();
        headers.SetString("idempotency-key", idempotencyKey.ToString());
        headers.SetString("x-outbox-occurred-on", occurredOn.ToString("O"));
        return producer.ProduceAsync(topic, Guid.NewGuid().ToString(), message, headers);
    }
}
