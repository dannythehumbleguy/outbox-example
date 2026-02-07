using KafkaFlow;
using KafkaFlow.Middlewares.Serializer.Resolvers;
using PaymentService.Application.Events;

namespace PaymentService.Infrastructure.Messaging;

public class CustomMessageTypeResolver : IMessageTypeResolver
{
    private const string MessageType = "Message-Type";
    private readonly Dictionary<string, Type> _typeMap;

    public CustomMessageTypeResolver()
    {
        _typeMap = BuildTypeMap();
    }

    private Dictionary<string, Type> BuildTypeMap()
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var messageTypes = GetAllMessageTypes();

        foreach (var type in messageTypes)
        {
            result.TryAdd(type.Name, type);
        }

        return result;
    }

    private IEnumerable<Type> GetAllMessageTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IKafkaFlowMessage).IsAssignableFrom(t));
    }

    public ValueTask<Type> OnConsumeAsync(IMessageContext context)
    {
        var className = context.Headers.GetString(MessageType);
        if (string.IsNullOrEmpty(className))
            throw new InvalidOperationException("Message-Type header is missing");

        if (_typeMap.TryGetValue(className, out var type))
            return ValueTask.FromResult(type);

        throw new InvalidOperationException($"Cannot resolve message type: {className}");
    }

    public ValueTask OnProduceAsync(IMessageContext context)
    {
        if (context.Message.Value is null)
            return ValueTask.CompletedTask;

        var messageType = context.Message.Value.GetType();
        context.Headers.SetString(MessageType, messageType.Name);
        return ValueTask.CompletedTask;
    }
}
