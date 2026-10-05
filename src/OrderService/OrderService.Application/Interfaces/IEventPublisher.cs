namespace OrderService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(string topic, string key, T message, Guid idempotencyKey, DateTimeOffset occurredOn) where T : class;
}
