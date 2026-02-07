namespace PaymentService.Application.Constants;

public static class KafkaTopics
{
    public const string OrderEvents = "order-events";
}

public static class KafkaConsumerGroups
{
    public const string PaymentService = "payment-service";
}
