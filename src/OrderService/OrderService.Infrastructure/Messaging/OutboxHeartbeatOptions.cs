namespace OrderService.Infrastructure.Messaging;

public class OutboxHeartbeatOptions
{
    public const string SectionName = "OutboxHeartbeat";

    public int ProcessingTimeoutSeconds { get; set; } = 30;
    public int PollingIntervalSeconds { get; set; } = 10;
}
