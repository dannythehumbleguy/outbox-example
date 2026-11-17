using KafkaFlow;
using KafkaFlow.Producers;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Messaging;

public class KafkaEventPublisher(IProducerAccessor producerAccessor, OutboxPublishContext publishContext) : IEventPublisher
{
    public Task PublishAsync<T>(string topic, string key, T message, Guid idempotencyKey, DateTimeOffset occurredOn) where T : class
    {
        var producer = producerAccessor.GetProducer(KafkaProducers.OrderEvents);
        var headers = new MessageHeaders();
        headers.SetString("idempotency-key", idempotencyKey.ToString());
        headers.SetString("x-outbox-occurred-on", occurredOn.ToString("O"));
        if (publishContext.RequestStartedAt is { } startedAt)
            headers.SetString("x-request-started-at", startedAt.ToString("O"));
        return producer.ProduceAsync(topic, key, message, headers);
    }
}
