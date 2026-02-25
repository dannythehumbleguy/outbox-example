namespace OrderService.Infrastructure.Messaging;

public class KafkaProducerOptions
{
    public const string SectionName = "Kafka:Producer";

    public int MessageSendMaxRetries { get; set; } = 3;
    public int MessageTimeoutMs { get; set; } = 5000;
    public int RequestTimeoutMs { get; set; } = 2000;
}
