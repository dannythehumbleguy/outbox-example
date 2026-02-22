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
        const string selectSql = """
            SELECT id AS Id, occurred_on AS OccurredOn, type AS Type, payload AS Payload, status AS Status
            FROM orders.outbox_messages
            WHERE status = 'Created'
            LIMIT 1
            """;

        const string updateSql = """
            UPDATE orders.outbox_messages
            SET status = 'Processed'
            WHERE id = @Id
            """;

        OutboxMessage? message;
        using (var connection = connectionFactory.CreateConnection())
        {
            message = await connection.QuerySingleOrDefaultAsync<OutboxMessage>(selectSql);
        }

        if (message is null) return;

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
            await handler.HandleAsync(message.Payload);

            using var connection = connectionFactory.CreateConnection();
            await connection.ExecuteAsync(updateSql, new { message.Id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish outbox message {Id} of type {Type}", message.Id, message.Type);
        }
    }
}
