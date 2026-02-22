using KafkaFlow.Producers;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class KafkaEventPublisher(IProducerAccessor producerAccessor) : IEventPublisher
{
    public Task PublishAsync<T>(string topic, T message) where T : class
    {
        var producer = producerAccessor.GetProducer(KafkaProducers.OrderEvents);
        producer.Produce(topic, Guid.NewGuid().ToString(), message);
        return Task.CompletedTask;
    }
}
