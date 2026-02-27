namespace OrderService.Infrastructure.Messaging;

public class OutboxCleanupOptions
{
    public const string SectionName = "OutboxCleanup";

    public int BatchSize { get; set; } = 1000;
    public int RetentionMinutes { get; set; } = 1440;
    public int PollingIntervalSeconds { get; set; } = 3600;
}
