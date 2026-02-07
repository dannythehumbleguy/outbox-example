namespace OrderService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(string topic, T message) where T : class;
}
