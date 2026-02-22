using KafkaFlow;
using KafkaFlow.Producers;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class KafkaEventPublisher(IProducerAccessor producerAccessor) : IEventPublisher
{
    public Task PublishAsync<T>(string topic, T message, Guid idempotencyKey) where T : class
    {
        var producer = producerAccessor.GetProducer(KafkaProducers.OrderEvents);
        var headers = new MessageHeaders();
        headers.SetString("idempotency-key", idempotencyKey.ToString());
        return producer.ProduceAsync(topic, Guid.NewGuid().ToString(), message, headers);
    }
}
