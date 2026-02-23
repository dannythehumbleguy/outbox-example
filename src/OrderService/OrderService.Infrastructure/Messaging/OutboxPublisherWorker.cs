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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxMessageAsync();
        }
    }

    private async Task ProcessOutboxMessageAsync()
    {
        OutboxMessage? message;
        using (var connection = connectionFactory.CreateConnection())
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var selectSql = 
                $"""
                 SELECT id AS Id, occurred_on AS OccurredOn, type AS Type, payload AS Payload, status AS Status
                 FROM orders.outbox_messages
                 WHERE status = '{nameof(OutboxMessageStatus.Created)}'
                 LIMIT 1
                 FOR UPDATE SKIP LOCKED
                 """;
            message = await connection.QuerySingleOrDefaultAsync<OutboxMessage>(selectSql, transaction: transaction);

            if (message is null) return;

            
            var processingUpdateSql = 
                $"""
                 UPDATE orders.outbox_messages
                 SET status = '{nameof(OutboxMessageStatus.Processing)}'
                 WHERE id = @Id
                 """;
            await connection.ExecuteAsync(processingUpdateSql, new { message.Id }, transaction: transaction);
            transaction.Commit();
        }

        using var scope = scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IOutboxMessageHandler>>();
        var handler = handlers.FirstOrDefault(h => h.MessageType == message.Type);

        if (handler is null)
        {
            logger.LogWarning("No handler registered for outbox message type: {Type}", message.Type);
            return;
        }

        try
        {
            await handler.HandleAsync(message.Payload, message.Id);

            using var connection = connectionFactory.CreateConnection();
            var processedUpdateSql = 
                $"""
                 UPDATE orders.outbox_messages
                 SET status = '{nameof(OutboxMessageStatus.Processed)}'
                 WHERE id = @Id
                 """;

            await connection.ExecuteAsync(processedUpdateSql, new { message.Id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish outbox message {Id} of type {Type}", message.Id, message.Type);
        }
    }
}
