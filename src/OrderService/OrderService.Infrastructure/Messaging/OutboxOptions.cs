namespace OrderService.Infrastructure.Messaging;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int MaxRetries { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 1;
}
