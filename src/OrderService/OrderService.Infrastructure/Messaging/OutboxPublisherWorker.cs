using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Messaging;

public class OutboxPublisherWorker(
    IDbConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxMessagesAsync();
        }
    }

    private async Task ProcessOutboxMessagesAsync()
    {
        List<OutboxMessage> messages;
        using (var connection = connectionFactory.CreateConnection())
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();

            messages = (await connection.QueryAsync<OutboxMessage>(
                $"""
                 SELECT id AS Id, occurred_on AS OccurredOn, type AS Type, payload AS Payload, status AS Status,
                        processed_at AS ProcessedAt, retry_count AS RetryCount, max_retries AS MaxRetries,
                        last_attempted_at AS LastAttemptedAt, next_retry_at AS NextRetryAt
                 FROM orders.outbox_messages
                 WHERE status IN ('{nameof(OutboxMessageStatus.Created)}', '{nameof(OutboxMessageStatus.Failed)}')
                   AND retry_count < max_retries
                   AND next_retry_at < now()
                 LIMIT {BatchSize}
                 FOR UPDATE SKIP LOCKED
                 """,
                transaction: transaction)).AsList();

            if (messages.Count == 0) return;

            logger.LogInformation("Picked up {Count} outbox message(s) for processing", messages.Count);

            await connection.ExecuteAsync(
                $"""
                 UPDATE orders.outbox_messages
                 SET status = '{nameof(OutboxMessageStatus.Processing)}', started_processing_at = @Now
                 WHERE id = ANY(@Ids)
                 """,
                new { Ids = messages.Select(m => m.Id).ToArray(), Now = DateTimeOffset.UtcNow },
                transaction: transaction);

            transaction.Commit();
        }

        using var scope = scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IOutboxMessageHandler>>();
        var processedMessages = new List<OutboxMessage>();
        var failedMessages = new List<OutboxMessage>();

        foreach (var message in messages)
        {
            var handler = handlers.FirstOrDefault(h => h.MessageType == message.Type);

            if (handler is null)
            {
                logger.LogWarning("No handler registered for outbox message type: {Type}", message.Type);
                continue;
            }

            try
            {
                await handler.HandleAsync(message.Payload, message.Id);
                processedMessages.Add(message);
                logger.LogInformation("Successfully published outbox message {Id} of type {Type}", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {Id} of type {Type}", message.Id, message.Type);
                failedMessages.Add(message);
            }
        }

        using var updateConnection = connectionFactory.CreateConnection();

        if (processedMessages.Count > 0)
        {
            logger.LogInformation("Marked {Count} outbox message(s) as Processed", processedMessages.Count);
            await updateConnection.ExecuteAsync(
                $"""
                 UPDATE orders.outbox_messages
                 SET status = '{nameof(OutboxMessageStatus.Processed)}', processed_at = @ProcessedAt
                 WHERE id = ANY(@Ids)
                 """,
                new { Ids = processedMessages.Select(m => m.Id).ToArray(), ProcessedAt = DateTimeOffset.UtcNow });
        }

        if (failedMessages.Count > 0)
        {
            await updateConnection.ExecuteAsync(
                $"""
                 UPDATE orders.outbox_messages
                 SET status = '{nameof(OutboxMessageStatus.Failed)}',
                     last_attempted_at = now(),
                     next_retry_at = now() + (INTERVAL '1 second' * @InitialRetryDelaySeconds * POWER(2, retry_count)),
                     retry_count = retry_count + 1
                 WHERE id = ANY(@Ids)
                 """,
                new { Ids = failedMessages.Select(m => m.Id).ToArray(), options.Value.InitialRetryDelaySeconds });
        }
    }
}
