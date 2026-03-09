namespace OrderService.Application.Interfaces;

public interface IOutboxMessageHandler
{
    string MessageType { get; }
    Task PublishAsync(string payload, Guid messageId);
}
