using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Messaging;

namespace OrderService.Infrastructure.Repositories;

public class OutboxMessageRepository(IOptions<OutboxOptions> options) : IOutboxMessageRepository
{
    public async Task CreateAsync<T>(T payload, IDbConnection connection, IDbTransaction transaction) where T : class
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTimeOffset.UtcNow,
            Type = typeof(T).Name,
            Payload = JsonSerializer.Serialize(payload),
            MaxRetries = options.Value.MaxRetries
        };

        const string sql = """
            INSERT INTO orders.outbox_messages (id, occurred_on, type, payload, status, retry_count, max_retries, next_retry_at)
            VALUES (@Id, @OccurredOn, @Type, @Payload::jsonb, @Status, @RetryCount, @MaxRetries, @NextRetryAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            message.Id,
            message.OccurredOn,
            message.Type,
            message.Payload,
            Status = message.Status.ToString(),
            message.RetryCount,
            message.MaxRetries,
            message.NextRetryAt
        }, transaction);
    }
}
