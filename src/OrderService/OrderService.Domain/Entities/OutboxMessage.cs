using System.Text.Json.Serialization;

namespace OrderService.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Created;
    public DateTimeOffset? StartedProcessingAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; }
    public DateTimeOffset? LastAttemptedAt { get; set; }
    public DateTimeOffset NextRetryAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TraceContext { get; set; }
}


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutboxMessageStatus
{
    Created,
    Processing,
    Processed,
    Failed
}
