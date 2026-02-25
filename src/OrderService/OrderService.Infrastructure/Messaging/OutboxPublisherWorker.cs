using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Messaging;

public class OutboxPublisherWorker(
    IDbConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
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
                 SELECT id AS Id, occurred_on AS OccurredOn, type AS Type, payload AS Payload, status AS Status, processed_at AS ProcessedAt
                 FROM orders.outbox_messages
                 WHERE status = '{nameof(OutboxMessageStatus.Created)}'
                 LIMIT {BatchSize}
                 FOR UPDATE SKIP LOCKED
                 """,
                transaction: transaction)).AsList();

            if (messages.Count == 0) return;

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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {Id} of type {Type}", message.Id, message.Type);
            }
        }

        if (processedMessages.Count == 0) return;

        using var updateConnection = connectionFactory.CreateConnection();
        await updateConnection.ExecuteAsync(
            $"""
             UPDATE orders.outbox_messages
             SET status = '{nameof(OutboxMessageStatus.Processed)}', processed_at = @ProcessedAt
             WHERE id = ANY(@Ids)
             """,
            new { Ids = processedMessages.Select(m => m.Id).ToArray(), ProcessedAt = DateTimeOffset.UtcNow });
    }
}
