namespace OrderService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(string topic, T message, Guid idempotencyKey, DateTimeOffset occurredOn) where T : class;
}
