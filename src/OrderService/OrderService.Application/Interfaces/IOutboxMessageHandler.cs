namespace OrderService.Application.Interfaces;

public interface IOutboxMessageHandler
{
    string MessageType { get; }
    Task HandleAsync(string payload, Guid messageId);
}
