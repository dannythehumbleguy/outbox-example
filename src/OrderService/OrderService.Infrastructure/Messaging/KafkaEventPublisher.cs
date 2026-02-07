using KafkaFlow.Producers;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class KafkaEventPublisher(IProducerAccessor producerAccessor) : IEventPublisher
{
    public async Task PublishAsync<T>(string topic, T message) where T : class
    {
        var producer = producerAccessor.GetProducer(KafkaProducers.OrderEvents);
        await producer.ProduceAsync(topic, Guid.NewGuid().ToString(), message);
    }
}
