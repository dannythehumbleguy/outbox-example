namespace OrderService.Application.Interfaces;

public interface IOutboxMessageProducer
{
    string MessageType { get; }
    Task PublishAsync(string payload, Guid messageId, DateTimeOffset occurredOn);
}
