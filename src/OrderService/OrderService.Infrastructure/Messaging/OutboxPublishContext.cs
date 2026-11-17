namespace OrderService.Infrastructure.Messaging;

public class OutboxPublishContext
{
    public DateTimeOffset? RequestStartedAt { get; set; }
}
