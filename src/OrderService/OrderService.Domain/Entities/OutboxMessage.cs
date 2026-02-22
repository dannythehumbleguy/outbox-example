using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderService.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Created;

    public static OutboxMessage From<T>(T payload) where T : class => new()
    {
        Id = Guid.NewGuid(),
        OccurredOn = DateTimeOffset.UtcNow,
        Type = typeof(T).Name,
        Payload = JsonSerializer.Serialize(payload)
    };
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutboxMessageStatus
{
    Created,
    Processed
}
